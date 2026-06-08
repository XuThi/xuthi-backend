using Contracts;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Order.Orders.Features.GetShippingSettings;
using Order.Orders.Models;
using ProductCatalog.Data;

namespace Order.Orders.Features.CalculateShipping;

public record CalculateShippingItem(Guid ProductId, Guid VariantId, int Quantity);

public record CalculateShippingQuery(
    PaymentMethod PaymentMethod, 
    string ShippingCity, 
    string ShippingWard,
    List<CalculateShippingItem> Items,
    string? ShippingDistrict = null
) : IQuery<CalculateShippingResult>;

public record CalculateShippingResult(decimal ShippingFee, bool IsGhnUsed);

internal class CalculateShippingHandler(
    ISender sender,
    HttpClient httpClient,
    ProductCatalogDbContext catalogDb,
    IConfiguration configuration)
    : IQueryHandler<CalculateShippingQuery, CalculateShippingResult>
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, List<GhnDistrict>> DistrictsCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, List<GhnWard>> WardsCache = new();
    private static List<GhnProvince>? _provincesCache;
    private static readonly SemaphoreSlim CacheSemaphore = new(1, 1);

    public async Task<CalculateShippingResult> Handle(CalculateShippingQuery request, CancellationToken cancellationToken)
    {
        // 1. Get shipping settings
        var settings = await sender.Send(new GetShippingSettingsQuery(), cancellationToken);
        if (!settings.Enabled)
        {
            return new CalculateShippingResult(0, false);
        }

        // 2. If payment method is not COD, shipping fee is 0
        if (request.PaymentMethod != PaymentMethod.CashOnDelivery)
        {
            return new CalculateShippingResult(0, false);
        }

        // Validate input city and ward parameters.
        if (string.IsNullOrWhiteSpace(request.ShippingCity) || string.IsNullOrWhiteSpace(request.ShippingWard))
        {
            var isHcm = !string.IsNullOrWhiteSpace(request.ShippingCity) &&
                        (request.ShippingCity.Contains("Hồ Chí Minh", StringComparison.OrdinalIgnoreCase) ||
                         request.ShippingCity.Contains("Ho Chi Minh", StringComparison.OrdinalIgnoreCase));
            decimal fallback = isHcm ? settings.HcmFallbackRate : settings.NationalFallbackRate;
            return new CalculateShippingResult(fallback, false);
        }

        // 3. Payment method is COD.
        var isHcmCity = request.ShippingCity.Contains("Hồ Chí Minh", StringComparison.OrdinalIgnoreCase) ||
                        request.ShippingCity.Contains("Ho Chi Minh", StringComparison.OrdinalIgnoreCase);

        // Retrieve GHN Token securely from Configuration or Environment variable, fall back to settings
        var ghnToken = configuration["GHN:Token"] ?? configuration["GHN_TOKEN"] ?? Environment.GetEnvironmentVariable("GHN_TOKEN") ?? settings.GhnToken;

        // Check if GHN dynamic shipping is enabled and key is provided
        if (settings.UseGhn && !string.IsNullOrWhiteSpace(ghnToken))
        {
            try
            {
                // Dynamically resolve customer's selected checkout address to GHN Location IDs
                var resolvedLocation = await ResolveGhnLocationAsync(request.ShippingCity, request.ShippingWard, request.ShippingDistrict, ghnToken, cancellationToken);
                
                int toDistrictId = 0;
                string toWardCode = "";

                if (resolvedLocation.HasValue)
                {
                    toDistrictId = resolvedLocation.Value.DistrictId;
                    toWardCode = resolvedLocation.Value.WardCode;
                }
                else
                {
                    Console.WriteLine($"Could not resolve GHN location for {request.ShippingCity} - {request.ShippingWard}, falling back to default COD rates.");
                    decimal fallbackFee = isHcmCity ? settings.HcmFallbackRate : settings.NationalFallbackRate;
                    return new CalculateShippingResult(fallbackFee, false);
                }

                // Load product physical dimensions/weights from catalogDb
                var productIds = request.Items?.Select(i => i.ProductId).Distinct().ToList() ?? new List<Guid>();
                var productsMap = productIds.Count > 0
                    ? await catalogDb.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken)
                    : new Dictionary<Guid, ProductCatalog.Products.Models.Product>();

                // Flatten items to compute alternating weights and stacked height
                var flatList = new List<ProductCatalog.Products.Models.Product>();
                if (request.Items != null)
                {
                    foreach (var item in request.Items)
                    {
                        if (productsMap.TryGetValue(item.ProductId, out var product))
                        {
                            for (int q = 0; q < item.Quantity; q++)
                            {
                                flatList.Add(product);
                            }
                        }
                    }
                }

                int totalWeightGrams = 0;
                int packageLength = settings.PackageLengthCm;
                int packageWidth = settings.PackageWidthCm;
                int packageHeight = 0;

                if (flatList.Count > 0)
                {
                    // Existing products created before the dimensions migration can have zero values.
                    // GHN rejects packages with zero weight or dimensions, so normalize each product.
                    var firstProduct = NormalizePackageDimensions(flatList[0], settings);
                    totalWeightGrams = firstProduct.WeightGrams;
                    packageLength = firstProduct.LengthCm;
                    packageWidth = firstProduct.WidthCm;
                    packageHeight = firstProduct.HeightCm;

                    for (int i = 1; i < flatList.Count; i++) // 0-indexed: index 1 is 2nd item, etc.
                    {
                        var currentProduct = NormalizePackageDimensions(flatList[i], settings);
                        int itemOrdinal = i + 1; // 1-based index (2nd item, 3rd, etc.)
                        
                        if (itemOrdinal % 2 == 0) // Even steps (2nd, 4th, 6th...)
                        {
                            totalWeightGrams += (int)Math.Round(currentProduct.WeightGrams * 0.5);
                        }
                        else // Odd steps (3rd, 5th, 7th...)
                        {
                            totalWeightGrams += currentProduct.WeightGrams;
                        }

                        // Stacking dimensions
                        packageHeight += currentProduct.HeightCm;
                        packageLength = Math.Max(packageLength, currentProduct.LengthCm);
                        packageWidth = Math.Max(packageWidth, currentProduct.WidthCm);
                    }
                }
                else
                {
                    totalWeightGrams = settings.PackageWeightGrams;
                    packageLength = settings.PackageLengthCm;
                    packageWidth = settings.PackageWidthCm;
                    packageHeight = settings.PackageHeightCm;
                }

                totalWeightGrams = Math.Max(totalWeightGrams, settings.PackageWeightGrams);
                packageLength = Math.Max(packageLength, settings.PackageLengthCm);
                packageWidth = Math.Max(packageWidth, settings.PackageWidthCm);
                packageHeight = Math.Max(packageHeight, settings.PackageHeightCm);

                // Resolve warehouse origin location dynamically from WarehouseCityName and WarehouseWardName
                int fromDistrictId = settings.GhnFromDistrictId;
                if (!string.IsNullOrWhiteSpace(settings.WarehouseCityName) && !string.IsNullOrWhiteSpace(settings.WarehouseWardName))
                {
                    var resolvedOrigin = await ResolveGhnLocationAsync(
                        settings.WarehouseCityName, 
                        settings.WarehouseWardName, 
                        settings.UseThreeLevelAddress ? settings.WarehouseDistrictName : null,
                        ghnToken, 
                        cancellationToken);
                    if (resolvedOrigin.HasValue)
                    {
                        fromDistrictId = resolvedOrigin.Value.DistrictId;
                    }
                    else
                    {
                        Console.WriteLine($"Could not resolve GHN warehouse location for {settings.WarehouseCityName} - {settings.WarehouseWardName}, falling back to default district ID {fromDistrictId}.");
                    }
                }

                var requestBody = new
                {
                    service_type_id = 2, // Giao hang chuan
                    from_district_id = fromDistrictId,
                    to_district_id = toDistrictId,
                    to_ward_code = toWardCode,
                    height = packageHeight,
                    length = packageLength,
                    width = packageWidth,
                    weight = totalWeightGrams,
                    insurance_value = 0,
                    coupon = (string?)null
                };

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/fee");
                httpRequest.Headers.Add("Token", ghnToken);
                
                if (settings.GhnShopId > 0)
                {
                    httpRequest.Headers.Add("ShopId", settings.GhnShopId.ToString());
                }

                httpRequest.Content = JsonContent.Create(requestBody);

                var response = await httpClient.SendAsync(httpRequest, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadFromJsonAsync<GhnFeeResponse>(cancellationToken: cancellationToken);
                    if (responseJson != null && responseJson.Code == 200 && responseJson.Data != null)
                    {
                        return new CalculateShippingResult(responseJson.Data.Total, true);
                    }
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"GHN API returned error: {response.StatusCode} - {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception calling GHN API: {ex.Message}");
            }

            // Fallback to configured settings fallback rules if GHN fails
        }

        // Configurable fallback logic (HCMC COD fee vs National COD fee)
        decimal fee = isHcmCity ? settings.HcmFallbackRate : settings.NationalFallbackRate;
        return new CalculateShippingResult(fee, false);
    }

    private async Task<(int DistrictId, string WardCode)?> ResolveInDistrictAsync(
        GhnDistrict district,
        string cleanWard,
        string token,
        CancellationToken ct)
    {
        if (!WardsCache.TryGetValue(district.DistrictID, out var wards))
        {
            using var wardRequest = new HttpRequestMessage(HttpMethod.Post, "https://online-gateway.ghn.vn/shiip/public-api/master-data/ward");
            wardRequest.Headers.Add("Token", token);
            wardRequest.Content = JsonContent.Create(new { district_id = district.DistrictID });
            var wardResponse = await httpClient.SendAsync(wardRequest, ct);
            if (wardResponse.IsSuccessStatusCode)
            {
                var wardResult = await wardResponse.Content.ReadFromJsonAsync<GhnWardResponse>(cancellationToken: ct);
                if (wardResult != null && wardResult.Code == 200 && wardResult.Data != null)
                {
                    wards = wardResult.Data;
                    WardsCache[district.DistrictID] = wards;
                }
            }
        }

        if (wards != null)
        {
            // Exact clean match first
            var matchWard = wards.FirstOrDefault(w => 
                CleanLocationName(w.WardName) == cleanWard ||
                (w.NameExtension != null && w.NameExtension.Any(ext => CleanLocationName(ext) == cleanWard)));

            if (matchWard != null)
            {
                return (district.DistrictID, matchWard.WardCode);
            }

            // Fuzzy fallback: contains-based matching (handles GHN vs dvhcvn naming differences)
            if (!string.IsNullOrEmpty(cleanWard) && cleanWard.Length >= 3)
            {
                matchWard = wards.FirstOrDefault(w =>
                {
                    var ghnClean = CleanLocationName(w.WardName);
                    return ghnClean.Contains(cleanWard) || cleanWard.Contains(ghnClean);
                });
                if (matchWard != null)
                {
                    Console.WriteLine($"Fuzzy ward match: '{cleanWard}' -> '{matchWard.WardName}' in district {district.DistrictName}");
                    return (district.DistrictID, matchWard.WardCode);
                }
            }
        }

        return null;
    }

    private static PackageDimensions NormalizePackageDimensions(
        ProductCatalog.Products.Models.Product product,
        ShippingSettingsDto settings)
    {
        return new PackageDimensions(
            WeightGrams: product.WeightGrams > 0 ? product.WeightGrams : settings.PackageWeightGrams,
            LengthCm: product.LengthCm > 0 ? product.LengthCm : settings.PackageLengthCm,
            WidthCm: product.WidthCm > 0 ? product.WidthCm : settings.PackageWidthCm,
            HeightCm: product.HeightCm > 0 ? product.HeightCm : settings.PackageHeightCm
        );
    }

    private readonly record struct PackageDimensions(
        int WeightGrams,
        int LengthCm,
        int WidthCm,
        int HeightCm);

    private async Task<(int DistrictId, string WardCode)?> ResolveInProvinceAsync(
        GhnProvince province,
        string cleanWard,
        string? cleanDistrict,
        string token,
        CancellationToken ct)
    {
        if (!DistrictsCache.TryGetValue(province.ProvinceID, out var districts))
        {
            using var distRequest = new HttpRequestMessage(HttpMethod.Post, "https://online-gateway.ghn.vn/shiip/public-api/master-data/district");
            distRequest.Headers.Add("Token", token);
            distRequest.Content = JsonContent.Create(new { province_id = province.ProvinceID });
            var distResponse = await httpClient.SendAsync(distRequest, ct);
            if (distResponse.IsSuccessStatusCode)
            {
                var distResult = await distResponse.Content.ReadFromJsonAsync<GhnDistrictResponse>(cancellationToken: ct);
                if (distResult != null && distResult.Code == 200 && distResult.Data != null)
                {
                    districts = distResult.Data;
                    DistrictsCache[province.ProvinceID] = districts;
                }
            }
        }

        if (districts == null || districts.Count == 0) return null;

        // If cleanDistrict is specified, try to find the matched district first
        if (!string.IsNullOrWhiteSpace(cleanDistrict))
        {
            // Exact match
            var matchDistrict = districts.FirstOrDefault(d => 
                CleanLocationName(d.DistrictName) == cleanDistrict ||
                (d.NameExtension != null && d.NameExtension.Any(ext => CleanLocationName(ext) == cleanDistrict)));

            // Fuzzy fallback for district: contains-based
            if (matchDistrict == null && cleanDistrict.Length >= 2)
            {
                matchDistrict = districts.FirstOrDefault(d =>
                {
                    var ghnClean = CleanLocationName(d.DistrictName);
                    return ghnClean.Contains(cleanDistrict) || cleanDistrict.Contains(ghnClean);
                });
                if (matchDistrict != null)
                    Console.WriteLine($"Fuzzy district match: '{cleanDistrict}' -> '{matchDistrict.DistrictName}'");
            }

            if (matchDistrict != null)
            {
                var resolved = await ResolveInDistrictAsync(matchDistrict, cleanWard, token, ct);
                if (resolved.HasValue)
                {
                    return resolved.Value;
                }
                // District matched but ward not found in it — fall through to brute-force scan
                Console.WriteLine($"District '{matchDistrict.DistrictName}' matched but ward '{cleanWard}' not found in it. Scanning all districts.");
            }
        }

        // Loop through all districts (fallback or 2-level mode)
        foreach (var district in districts)
        {
            var resolved = await ResolveInDistrictAsync(district, cleanWard, token, ct);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }
        }

        return null;
    }

    private async Task<(int DistrictId, string WardCode)?> ResolveGhnLocationAsync(
        string city, 
        string ward, 
        string? district,
        string token, 
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(ward))
        {
            return null;
        }

        // Backward compatibility / DB compatibility: Parse combined ward format if district is not provided but ward contains a comma
        if (string.IsNullOrWhiteSpace(district) && ward.Contains(','))
        {
            var parts = ward.Split(',');
            if (parts.Length >= 2)
            {
                district = parts[0].Trim();
                ward = parts[1].Trim();
            }
        }

        try
        {
            if (_provincesCache == null)
            {
                await CacheSemaphore.WaitAsync(ct);
                try
                {
                    if (_provincesCache == null)
                    {
                        using var provRequest = new HttpRequestMessage(HttpMethod.Get, "https://online-gateway.ghn.vn/shiip/public-api/master-data/province");
                        provRequest.Headers.Add("Token", token);
                        var provResponse = await httpClient.SendAsync(provRequest, ct);
                        if (provResponse.IsSuccessStatusCode)
                        {
                            var provResult = await provResponse.Content.ReadFromJsonAsync<GhnProvinceResponse>(cancellationToken: ct);
                            if (provResult != null && provResult.Code == 200 && provResult.Data != null)
                            {
                                _provincesCache = provResult.Data;
                            }
                        }
                    }
                }
                finally
                {
                    CacheSemaphore.Release();
                }
            }

            if (_provincesCache == null || _provincesCache.Count == 0) return null;

            var cleanCity = CleanLocationName(city);
            var cleanWard = CleanLocationName(ward);
            var cleanDistrict = !string.IsNullOrWhiteSpace(district) ? CleanLocationName(district) : null;

            var province = _provincesCache.FirstOrDefault(p => 
                CleanLocationName(p.ProvinceName).Contains(cleanCity) || 
                cleanCity.Contains(CleanLocationName(p.ProvinceName)) ||
                (p.NameExtension != null && p.NameExtension.Any(ext => CleanLocationName(ext).Contains(cleanCity))));

            if (province != null)
            {
                var resolved = await ResolveInProvinceAsync(province, cleanWard, cleanDistrict, token, ct);
                if (resolved.HasValue)
                {
                    return resolved.Value;
                }
            }

            // TODO: I will clean this later
            if (cleanCity == "da nang")
            {
                var fallbackProvinces = new List<GhnProvince>();
                
                var qn = _provincesCache.FirstOrDefault(p => CleanLocationName(p.ProvinceName).Contains("quang nam"));
                if (qn != null) fallbackProvinces.Add(qn);
                
                var tth = _provincesCache.FirstOrDefault(p => CleanLocationName(p.ProvinceName).Contains("thua thien hue") || CleanLocationName(p.ProvinceName).Contains("hue"));
                if (tth != null) fallbackProvinces.Add(tth);

                foreach (var fbProvince in fallbackProvinces)
                {
                    var resolved = await ResolveInProvinceAsync(fbProvince, cleanWard, cleanDistrict, token, ct);
                    if (resolved.HasValue)
                    {
                        Console.WriteLine($"Resolved ward '{ward}' dynamically in fallback province '{fbProvince.ProvinceName}' instead of '{city}'");
                        return resolved.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resolving GHN location: {ex.Message}");
        }

        return null;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
        var stringBuilder = new System.Text.StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private static readonly System.Text.RegularExpressions.Regex _locationPrefixRegex =
        new System.Text.RegularExpressions.Regex(
            @"^(thanh pho|tinh|quan|huyen|thi xa|phuong|xa|thi tran|tp\.?|q\.?)\s+",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string CleanLocationName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var clean = RemoveDiacritics(name).ToLower().Trim();
        // Strip common administrative type prefixes from the START of the name only
        // This avoids mangling names that happen to contain these words mid-string
        // e.g. "Quan" should not strip from "Quang Nam", "xa" should not strip from "Phong Xa La"
        clean = _locationPrefixRegex.Replace(clean, "");
        return clean
            .Replace("-", " ")
            .Replace("  ", " ")
            .Trim();
    }
}

public class GhnProvinceResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    [JsonPropertyName("data")]
    public List<GhnProvince>? Data { get; set; }
}

public class GhnProvince
{
    [JsonPropertyName("ProvinceID")]
    public int ProvinceID { get; set; }
    [JsonPropertyName("ProvinceName")]
    public string ProvinceName { get; set; } = "";
    [JsonPropertyName("NameExtension")]
    public List<string>? NameExtension { get; set; }
}

public class GhnDistrictResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    [JsonPropertyName("data")]
    public List<GhnDistrict>? Data { get; set; }
}

public class GhnDistrict
{
    [JsonPropertyName("DistrictID")]
    public int DistrictID { get; set; }
    [JsonPropertyName("DistrictName")]
    public string DistrictName { get; set; } = "";
    [JsonPropertyName("NameExtension")]
    public List<string>? NameExtension { get; set; }
}

public class GhnWardResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    [JsonPropertyName("data")]
    public List<GhnWard>? Data { get; set; }
}

public class GhnWard
{
    [JsonPropertyName("WardCode")]
    public string WardCode { get; set; } = "";
    [JsonPropertyName("WardName")]
    public string WardName { get; set; } = "";
    [JsonPropertyName("NameExtension")]
    public List<string>? NameExtension { get; set; }
}

public class GhnFeeResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public GhnFeeData? Data { get; set; }
}

public class GhnFeeData
{
    [JsonPropertyName("total")]
    public decimal Total { get; set; }
}

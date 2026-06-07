using MediatR;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Order.Orders.Features.GetShippingSettings;

// TODO: Crazy ahh Code

public record ShippingSettingsDto(
    bool Enabled, 
    decimal FlatRate, 
    bool UseGhn = false, 
    string GhnToken = "", 
    int GhnShopId = 0,
    int GhnFromDistrictId = 1454,
    string GhnFromWardCode = "21012",
    int GhnToHcmDistrictId = 1442,
    string GhnToHcmWardCode = "20108",
    int GhnToNationalDistrictId = 1482,
    string GhnToNationalWardCode = "1A0107",
    decimal HcmFallbackRate = 18000,
    decimal NationalFallbackRate = 30000,
    int PackageWeightGrams = 1000,
    int PackageLengthCm = 28,
    int PackageWidthCm = 18,
    int PackageHeightCm = 9,
    string WarehouseCityCode = "12",
    string WarehouseCityName = "Thành phố Hồ Chí Minh",
    string WarehouseWardCode = "25363",
    string WarehouseWardName = "Phường Đông Hưng Thuận",
    bool UseThreeLevelAddress = false,
    string WarehouseDistrictCode = "",
    string WarehouseDistrictName = ""
);

public record GetShippingSettingsQuery() : IRequest<ShippingSettingsDto>;

internal class GetShippingSettingsHandler : IRequestHandler<GetShippingSettingsQuery, ShippingSettingsDto>
{
    private static readonly string SettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "shipping_settings.json");

    public Task<ShippingSettingsDto> Handle(GetShippingSettingsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<ShippingSettingsDto>(json);
                if (settings != null)
                {
                    // Clean up and populate defaults if fields are missing in an older JSON schema
                    return Task.FromResult(new ShippingSettingsDto(
                        Enabled: settings.Enabled,
                        FlatRate: settings.FlatRate,
                        UseGhn: settings.UseGhn,
                        GhnToken: settings.GhnToken ?? "",
                        GhnShopId: settings.GhnShopId,
                        GhnFromDistrictId: settings.GhnFromDistrictId == 0 ? 1454 : settings.GhnFromDistrictId,
                        GhnFromWardCode: string.IsNullOrWhiteSpace(settings.GhnFromWardCode) ? "21012" : settings.GhnFromWardCode,
                        GhnToHcmDistrictId: settings.GhnToHcmDistrictId == 0 ? 1442 : settings.GhnToHcmDistrictId,
                        GhnToHcmWardCode: string.IsNullOrWhiteSpace(settings.GhnToHcmWardCode) ? "20108" : settings.GhnToHcmWardCode,
                        GhnToNationalDistrictId: settings.GhnToNationalDistrictId == 0 ? 1482 : settings.GhnToNationalDistrictId,
                        GhnToNationalWardCode: string.IsNullOrWhiteSpace(settings.GhnToNationalWardCode) ? "1A0107" : settings.GhnToNationalWardCode,
                        HcmFallbackRate: settings.HcmFallbackRate == 0 ? 18000 : settings.HcmFallbackRate,
                        NationalFallbackRate: settings.NationalFallbackRate == 0 ? 30000 : settings.NationalFallbackRate,
                        PackageWeightGrams: settings.PackageWeightGrams == 0 ? 1000 : settings.PackageWeightGrams,
                        PackageLengthCm: settings.PackageLengthCm == 0 ? 28 : settings.PackageLengthCm,
                        PackageWidthCm: settings.PackageWidthCm == 0 ? 18 : settings.PackageWidthCm,
                        PackageHeightCm: settings.PackageHeightCm == 0 ? 9 : settings.PackageHeightCm,
                        WarehouseCityCode: string.IsNullOrWhiteSpace(settings.WarehouseCityCode) ? "12" : settings.WarehouseCityCode,
                        WarehouseCityName: string.IsNullOrWhiteSpace(settings.WarehouseCityName) ? "Thành phố Hồ Chí Minh" : settings.WarehouseCityName,
                        WarehouseWardCode: string.IsNullOrWhiteSpace(settings.WarehouseWardCode) ? "25363" : settings.WarehouseWardCode,
                        WarehouseWardName: string.IsNullOrWhiteSpace(settings.WarehouseWardName) ? "Phường Đông Hưng Thuận" : settings.WarehouseWardName,
                        UseThreeLevelAddress: settings.UseThreeLevelAddress,
                        WarehouseDistrictCode: settings.WarehouseDistrictCode ?? "",
                        WarehouseDistrictName: settings.WarehouseDistrictName ?? ""
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading shipping settings: {ex.Message}");
        }

        // Return full defaults
        return Task.FromResult(new ShippingSettingsDto(
            Enabled: true, 
            FlatRate: 30000, 
            UseGhn: false, 
            GhnToken: "", 
            GhnShopId: 0,
            GhnFromDistrictId: 1454,
            GhnFromWardCode: "21012",
            GhnToHcmDistrictId: 1442,
            GhnToHcmWardCode: "20108",
            GhnToNationalDistrictId: 1482,
            GhnToNationalWardCode: "1A0107",
            HcmFallbackRate: 18000,
            NationalFallbackRate: 30000,
            PackageWeightGrams: 1000,
            PackageLengthCm: 28,
            PackageWidthCm: 18,
            PackageHeightCm: 9,
            WarehouseCityCode: "12",
            WarehouseCityName: "Thành phố Hồ Chí Minh",
            WarehouseWardCode: "25363",
            WarehouseWardName: "Phường Đông Hưng Thuận",
            UseThreeLevelAddress: false,
            WarehouseDistrictCode: "",
            WarehouseDistrictName: ""
        ));
    }
}

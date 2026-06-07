using MediatR;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Order.Orders.Features.UpdateShippingSettings;

// TODO: Holy Lmao what kind of abormination is this
public record UpdateShippingSettingsCommand(
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
) : IRequest<bool>;

internal class UpdateShippingSettingsHandler : IRequestHandler<UpdateShippingSettingsCommand, bool>
{
    private static readonly string SettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "shipping_settings.json");

    public Task<bool> Handle(UpdateShippingSettingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var dto = new
            {
                request.Enabled,
                request.FlatRate,
                request.UseGhn,
                GhnToken = "",
                request.GhnShopId,
                request.GhnFromDistrictId,
                request.GhnFromWardCode,
                request.GhnToHcmDistrictId,
                request.GhnToHcmWardCode,
                request.GhnToNationalDistrictId,
                request.GhnToNationalWardCode,
                request.HcmFallbackRate,
                request.NationalFallbackRate,
                request.PackageWeightGrams,
                request.PackageLengthCm,
                request.PackageWidthCm,
                request.PackageHeightCm,
                request.WarehouseCityCode,
                request.WarehouseCityName,
                request.WarehouseWardCode,
                request.WarehouseWardName,
                request.UseThreeLevelAddress,
                request.WarehouseDistrictCode,
                request.WarehouseDistrictName
            };
            var json = JsonSerializer.Serialize(dto);
            File.WriteAllText(SettingsPath, json);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing shipping settings: {ex.Message}");
            return Task.FromResult(false);
        }
    }
}

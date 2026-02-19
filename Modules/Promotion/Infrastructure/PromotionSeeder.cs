using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Infrastructure.Data;

namespace Promotion.Infrastructure;

public static class PromotionSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var promoDb = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        var catalogDb = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();

        if (!await promoDb.Vouchers.AnyAsync())
        {
            var now = DateTime.UtcNow;
            promoDb.Vouchers.AddRange(
                new Voucher
                {
                    Id = Guid.NewGuid(),
                    Code = "XUTHI10",
                    Description = "Giam 10% cho don hang tu 500k",
                    Type = VoucherType.Percentage,
                    DiscountValue = 10,
                    MinimumOrderAmount = 500000,
                    MaximumDiscountAmount = 100000,
                    StartDate = now.AddDays(-7),
                    EndDate = now.AddMonths(2),
                    IsActive = true
                },
                new Voucher
                {
                    Id = Guid.NewGuid(),
                    Code = "FREESHIP",
                    Description = "Mien phi van chuyen",
                    Type = VoucherType.FreeShipping,
                    DiscountValue = 30000,
                    MinimumOrderAmount = 300000,
                    StartDate = now.AddDays(-7),
                    EndDate = now.AddMonths(2),
                    IsActive = true
                },
                new Voucher
                {
                    Id = Guid.NewGuid(),
                    Code = "BLACKFRIDAY",
                    Description = "Giam 15% mua sam Black Friday",
                    Type = VoucherType.Percentage,
                    DiscountValue = 15,
                    MinimumOrderAmount = 800000,
                    MaximumDiscountAmount = 150000,
                    StartDate = now.AddDays(-7),
                    EndDate = now.AddMonths(2),
                    IsActive = true
                }
            );
        }

        if (!await promoDb.SaleCampaigns.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var products = await catalogDb.Products
                .Include(p => p.Variants)
                .Include(p => p.Images)
                    .ThenInclude(pi => pi.Image)
                .OrderBy(p => p.CreatedAt)
                .Take(6)
                .ToListAsync();

            var blackFriday = new SaleCampaign
            {
                Id = Guid.NewGuid(),
                Name = "Black Friday",
                Slug = "black-friday",
                Description = "Uu dai cuc soc cho mua sam Black Friday",
                BannerImageUrl = "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/banner.jpg?alt=media&token=913e4ead-a710-4b1d-9111-1be0a973106a",
                Type = SaleCampaignType.SeasonalSale,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(14),
                IsActive = true,
                IsFeatured = true
            };

            var double11 = new SaleCampaign
            {
                Id = Guid.NewGuid(),
                Name = "11.11 Mega Sale",
                Slug = "11-11-mega-sale",
                Description = "Giam gia dac biet ngay 11.11",
                BannerImageUrl = "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/banner-double11.jpg?alt=media&token=913e4ead-a710-4b1d-9111-1be0a973106a",
                Type = SaleCampaignType.SeasonalSale,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(7),
                IsActive = true,
                IsFeatured = true
            };

            var campaignItems = new List<SaleCampaignItem>();
            var blackFridayProducts = products.Take(3).ToList();
            var double11Products = products.Skip(3).Take(3).ToList();

            foreach (var product in blackFridayProducts)
            {
                var price = product.Variants.FirstOrDefault()?.Price ?? 0m;
                if (price <= 0)
                {
                    continue;
                }

                var salePrice = Math.Round(price * 0.85m, 0);
                var discountPercent = Math.Round((1 - (salePrice / price)) * 100, 2);

                campaignItems.Add(new SaleCampaignItem
                {
                    Id = Guid.NewGuid(),
                    SaleCampaignId = blackFriday.Id,
                    ProductId = product.Id,
                    VariantId = null,
                    OriginalPrice = price,
                    SalePrice = salePrice,
                    DiscountPercentage = discountPercent,
                    MaxQuantity = null,
                    SoldQuantity = 0
                });
            }

            foreach (var product in double11Products)
            {
                var price = product.Variants.FirstOrDefault()?.Price ?? 0m;
                if (price <= 0)
                {
                    continue;
                }

                campaignItems.Add(new SaleCampaignItem
                {
                    Id = Guid.NewGuid(),
                    SaleCampaignId = double11.Id,
                    ProductId = product.Id,
                    VariantId = null,
                    OriginalPrice = price,
                    SalePrice = Math.Round(price * 0.9m, 0),
                    DiscountPercentage = Math.Round((1 - 0.9m) * 100, 2),
                    MaxQuantity = null,
                    SoldQuantity = 0
                });
            }

            promoDb.SaleCampaigns.AddRange(blackFriday, double11);
            promoDb.SaleCampaignItems.AddRange(campaignItems);
        }

        await promoDb.SaveChangesAsync();
    }
}

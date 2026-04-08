using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ProductCatalog.Data.Seed;

public static class ProductCatalogSeeder
{
    // Fixed GUIDs for seed data
    private static readonly Guid BrandId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();

        // Only seed if database is empty
        if (await db.Brands.AnyAsync()) return;

        // Seed brand
        var brand = new Brand
        {
            Id = BrandId,
            Name = "Xu Thi",
            UrlSlug = "xu-thi",
            Description = "Giày cao gót thương hiệu Xu Thi",
            LogoUrl = "https://res.cloudinary.com/dxlhncwp0/image/upload/v1769941817/logo_qlelti.svg"
        };
        db.Brands.Add(brand);
        // Seed category
        var category = new Category
        {
            Id = CategoryId,
            Name = "Giày cao gót",
            UrlSlug = "giay-cao-got",
            Description = "Các mẫu giày cao gót đẹp",
            ParentCategoryId = Guid.Empty,
            ImageUrl = "https://res.cloudinary.com/dxlhncwp0/image/upload/v1772559328/highheel_category_kt5qdq.jpg",
            ImagePublicId = "highheel_category_kt5qdq",
            SortOrder = 1
        };
        db.Categories.Add(category);

        // Seed VariantOption: Size
        var sizeOption = new VariantOption
        {
            Id = "size",
            Name = "Kích thước",
            DisplayType = "button",
            DefaultValue = "37",
            Values =
            [
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "size", Value = "34", DisplayValue = "Size 34", SortOrder = 1 },
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "size", Value = "35", DisplayValue = "Size 35", SortOrder = 2 },
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "size", Value = "36", DisplayValue = "Size 36", SortOrder = 3 },
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "size", Value = "37", DisplayValue = "Size 37", SortOrder = 4 },
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "size", Value = "38", DisplayValue = "Size 38", SortOrder = 5 },
            ]
        };
        db.VariantOptions.Add(sizeOption);

        // Seed VariantOption: Color
        var colorOption = new VariantOption
        {
            Id = "color",
            Name = "Màu sắc",
            DisplayType = "color",
            DefaultValue = "Đen",
            Values =
            [
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "color", Value = "Đen", DisplayValue = "Đen", SortOrder = 1 },
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "color", Value = "Trắng", DisplayValue = "Trắng", SortOrder = 2 },
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "color", Value = "Đỏ", DisplayValue = "Đỏ", SortOrder = 3 },
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "color", Value = "Hồng", DisplayValue = "Hồng", SortOrder = 4 },
                new VariantOptionValue { Id = Guid.NewGuid(), VariantOptionId = "color", Value = "Be", DisplayValue = "Be", SortOrder = 5 },
            ]
        };
        db.VariantOptions.Add(colorOption);


        // Product seed data from TypeScript
        var products = GetProductSeedData();
        var random = new Random(20260219);

        foreach (var (productData, index) in products.Select((p, i) => (p, i)))
        {
            var productId = Guid.NewGuid();
            var product = new Product
            {
                Id = productId,
                Name = productData.Name,
                UrlSlug = GenerateSlug(productData.Name) + "-" + productData.SkuPrefix.ToLowerInvariant(),
                Description = productData.Description,
                BrandId = BrandId,
                CategoryId = CategoryId,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            // Add ProductVariantOption link (this product uses Size option)
            product.VariantOptions.Add(new ProductVariantOption
            {
                ProductId = productId,
                VariantOptionId = "size",
                SortOrder = 1
            });

            var useColorOption = index < 3;
            if (useColorOption)
            {
                product.VariantOptions.Add(new ProductVariantOption
                {
                    ProductId = productId,
                    VariantOptionId = "color",
                    SortOrder = 2
                });
            }

            // Add images
            var sortOrder = 0;
            foreach (var imageUrl in productData.ImageUrls)
            {
                var imageId = Guid.NewGuid();
                var image = new Image
                {
                    Id = imageId,
                    Url = imageUrl,
                    CloudinaryPublicId = ExtractCloudinaryPublicId(imageUrl),
                    AltText = productData.Name
                };
                db.Images.Add(image);

                product.Images.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    ImageId = imageId,
                    AltText = productData.Name,
                    SortOrder = sortOrder++
                });
            }

            // Create one variant per size
            var sizes = new[] { "34", "35", "36", "37", "38" };
            var colors = useColorOption ? new[] { "Đen", "Trắng", "Đỏ" } : new[] { string.Empty };
            foreach (var size in sizes)
            {
                foreach (var color in colors)
                {
                    var variantId = Guid.NewGuid();
                    var sku = useColorOption
                        ? $"XT-{productData.SkuPrefix}-{size}-{GenerateSkuSegment(color)}"
                        : $"XT-{productData.SkuPrefix}-{size}";

                    var stockQuantity = random.NextDouble() < 0.2 ? 0 : random.Next(3, 26);
                    var compareAtPrice = Math.Round(productData.Price * 1.15m, 0, MidpointRounding.AwayFromZero);

                    var selections = new List<VariantOptionSelection>
                    {
                        new()
                        {
                            VariantId = variantId,
                            VariantOptionId = "size",
                            Value = size
                        }
                    };

                    if (useColorOption)
                    {
                        selections.Add(new VariantOptionSelection
                        {
                            VariantId = variantId,
                            VariantOptionId = "color",
                            Value = color
                        });
                    }

                    product.Variants.Add(new Variant
                    {
                        Id = variantId,
                        ProductId = productId,
                        Sku = sku,
                        BarCode = sku, // Use SKU as barcode for simplicity
                        Price = productData.Price,
                        CompareAtPrice = compareAtPrice,
                        StockQuantity = stockQuantity,
                        Description = useColorOption
                            ? $"{productData.Name} - Size {size} - {color}"
                            : $"{productData.Name} - Size {size}",
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        OptionSelections = selections
                    });
                }
            }

            db.Products.Add(product);
        }

        await db.SaveChangesAsync();
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.Replace("đ", "d").Replace("Đ", "d")
            .Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in slug)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c)
                != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        slug = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');
        return slug;
    }

    private static string GenerateSkuSegment(string value)
    {
        return value
            .Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            .Select(ch => ch is 'đ' or 'Đ' ? 'd' : ch)
            .Aggregate(new StringBuilder(), (sb, ch) =>
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToUpperInvariant(ch));
                }
                else if (sb.Length > 0 && sb[^1] != '-')
                {
                    sb.Append('-');
                }
                return sb;
            })
            .ToString()
            .Trim('-');
    }

    private static string? ExtractCloudinaryPublicId(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!uri.Host.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    private static List<ProductSeedData> GetProductSeedData() =>
    [
        new("Giày cao gót nữ màu bạc quai đính đá", "Giày cao gót sang trọng với quai đính đá lấp lánh", "GG1", 680000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888633/Juva-1_jjvkgj.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888658/Juva-12_zmbgss.jpg"
        ]),
        new ("Giày cao gót tiểu thư", "Giày cao gót phong cách tiểu thư thanh lịch", "GN1", 610000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888635/Juva-2_i8nnst.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888663/Juva-15_zbsgta.jpg"
        ]),
        new("Giày cao gót đen dây chéo phối dây đá", "Giày cao gót đen thiết kế dây chéo độc đáo", "GW4", 650000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888648/Juva-3_yv5oyf.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888663/Juva-16_ppuf0y.jpg"
        ]),
        new("Giày cao gót quai sang chảnh", "Giày cao gót với quai thiết kế sang chảnh", "GB1", 610000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888648/Juva-4_gdeg3o.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888662/Juva-14_euhtil.jpg"
        ]),
        new("Giày cao gót nude đế trong", "Giày cao gót màu nude với đế trong suốt hiện đại", "GN2", 610000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888649/Juva-5_za9sni.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888658/Juva-13_bjx1kw.jpg"
        ]),
        new("Giày cao gót đen quai đính đá", "Giày cao gót đen sang trọng với quai đính đá", "GB4", 670000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888649/Juva-6_npync6.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888657/Juva-11_ljvzrb.jpg"
        ]),
        new("Guốc đen sang chảnh đá 7 màu", "Guốc đen thiết kế sang chảnh với đá 7 màu lấp lánh", "GB3", 690000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888649/Juva-7_mqbtj9.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888664/Juva-17_aqf68s.jpg"
        ]),
        new("Giày cao gót đen quai đính đá", "Giày cao gót đen với quai đính đá tinh tế", "GB2", 650000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888649/Juva-8_odn5m2.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888665/Juva-18_ufyhdf.jpg"
        ]),
        new("Giày cao gót trắng quai chéo dây phối dây đá", "Giày cao gót trắng với thiết kế quai chéo phối đá", "GW2", 630000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888655/Juva-9_j0xswq.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888666/Juva-20_fptylh.jpg"
        ]),
        new("Giày cao gót trắng quai chéo đính đá", "Giày cao gót trắng thanh lịch với quai chéo đính đá", "GW1", 630000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888657/Juva-10_bgxkre.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888665/Juva-19_fcwfjh.jpg"
        ]),
        new("Giày cao gót bạc tiểu thư", "Giày cao gót bạc phong cách tiểu thư", "SP1", 580000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888669/Juva-21_hhbgis.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888669/Juva-22_wexziv.jpg"
        ]),
        new("Giày cao gót vàng đồng quai tỏng", "Giày cao gót vàng đồng thiết kế độc đáo", "SP2", 550000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888675/Juva-23_s9lxwf.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888676/Juva-24_kg3zvo.jpg"
        ]),
        new("Giày cao gót đính đá", "Giày cao gót đính đá lấp lánh", "SP3", 630000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888676/Juva-25_tzyu7a.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888676/Juva-26_f4oqof.jpg"
        ]),
        new("Giày cao quai trong (đen)", "Giày cao gót đen với quai trong suốt", "SP4", 550000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888676/Juva-27_kktwm7.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888676/Juva-28_jxa9pp.jpg"
        ]),
        new("Dép xỏ ngón", "Dép xỏ ngón thời trang", "SP5", 180000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888631/Juva-31_boahb5.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888631/Juva-32_stqsps.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888632/Juva-33_zy8yyj.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888631/Juva-34_xnu5gs.jpg"
        ]),
        new("Giày cao quai nhung", "Giày cao gót với quai nhung sang trọng", "SP6", 580000, [
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888632/Juva-29_txwiiq.jpg",
            "https://res.cloudinary.com/dxlhncwp0/image/upload/v1774888631/Juva-30_mjqwxv.jpg"
        ])
    ];
    
    private record ProductSeedData(string Name, string Description, string SkuPrefix, decimal Price, List<string> ImageUrls);
}

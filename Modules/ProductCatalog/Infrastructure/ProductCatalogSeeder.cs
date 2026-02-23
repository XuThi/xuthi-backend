using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.Infrastructure.Entity;

namespace ProductCatalog.Infrastructure;

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
            SortOrder = 1
        };
        db.Categories.Add(category);
        
        // Seed VariantOption: Size
        var sizeOption = new VariantOption
        {
            Id = "size",
            Name = "Kích thước",
            DisplayType = "dropdown",
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
                UpdatedAt = DateTime.UtcNow
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
                    CloudinaryPublicId = null, // Firebase URL, not Cloudinary
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
        // Simple Vietnamese-aware slug generation
        var slug = name.ToLowerInvariant()
            .Replace("á", "a").Replace("à", "a").Replace("ả", "a").Replace("ã", "a").Replace("ạ", "a")
            .Replace("ă", "a").Replace("ắ", "a").Replace("ằ", "a").Replace("ẳ", "a").Replace("ẵ", "a").Replace("ặ", "a")
            .Replace("â", "a").Replace("ấ", "a").Replace("ầ", "a").Replace("ẩ", "a").Replace("ẫ", "a").Replace("ậ", "a")
            .Replace("đ", "d")
            .Replace("é", "e").Replace("è", "e").Replace("ẻ", "e").Replace("ẽ", "e").Replace("ẹ", "e")
            .Replace("ê", "e").Replace("ế", "e").Replace("ề", "e").Replace("ể", "e").Replace("ễ", "e").Replace("ệ", "e")
            .Replace("í", "i").Replace("ì", "i").Replace("ỉ", "i").Replace("ĩ", "i").Replace("ị", "i")
            .Replace("ó", "o").Replace("ò", "o").Replace("ỏ", "o").Replace("õ", "o").Replace("ọ", "o")
            .Replace("ô", "o").Replace("ố", "o").Replace("ồ", "o").Replace("ổ", "o").Replace("ỗ", "o").Replace("ộ", "o")
            .Replace("ơ", "o").Replace("ớ", "o").Replace("ờ", "o").Replace("ở", "o").Replace("ỡ", "o").Replace("ợ", "o")
            .Replace("ú", "u").Replace("ù", "u").Replace("ủ", "u").Replace("ũ", "u").Replace("ụ", "u")
            .Replace("ư", "u").Replace("ứ", "u").Replace("ừ", "u").Replace("ử", "u").Replace("ữ", "u").Replace("ự", "u")
            .Replace("ý", "y").Replace("ỳ", "y").Replace("ỷ", "y").Replace("ỹ", "y").Replace("ỵ", "y")
            .Replace(" ", "-")
            .Replace("(", "").Replace(")", "");
        
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
    
    private static List<ProductSeedData> GetProductSeedData() =>
    [
        new("Giày cao gót nữ màu bạc quai đính đá", "Giày cao gót sang trọng với quai đính đá lấp lánh", "GG1", 680000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-1.jpg?alt=media&token=b38fd3b2-edf3-4277-87d8-7b4b0fb62f56",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-12.jpg?alt=media&token=e96193e5-e7d9-4a1a-ba70-f50849b4f165"
        ]),
        new("Giày cao gót tiểu thư", "Giày cao gót phong cách tiểu thư thanh lịch", "GN1", 610000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-2.jpg?alt=media&token=9d2eec14-b2be-43c0-b50a-712e46693902",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-15.jpg?alt=media&token=1e110646-6fd2-4634-ac0a-276b96374fb9"
        ]),
        new("Giày cao gót đen dây chéo phối dây đá", "Giày cao gót đen thiết kế dây chéo độc đáo", "GW4", 650000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-3.jpg?alt=media&token=5afa247e-412a-4695-8be4-14d462d95b7e",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-16.jpg?alt=media&token=a9b6fb01-ad89-4dc2-b1ac-d01aafb83db5"
        ]),
        new("Giày cao gót quai sang chảnh", "Giày cao gót với quai thiết kế sang chảnh", "GB1", 610000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-4.jpg?alt=media&token=1684294e-fe27-4afd-9a82-c5c03ca97902",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-14.jpg?alt=media&token=0b1f8c15-a3c9-4b57-ab73-33d998534544"
        ]),
        new("Giày cao gót nude đế trong", "Giày cao gót màu nude với đế trong suốt hiện đại", "GN2", 610000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-5.jpg?alt=media&token=9009ba11-c080-4211-b8b8-bc1a1ce8b00a",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-13.jpg?alt=media&token=c9dd9dda-fe41-4ca9-a3bf-0bc708ec2201"
        ]),
        new("Giày cao gót đen quai đính đá", "Giày cao gót đen sang trọng với quai đính đá", "GB4", 670000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-6.jpg?alt=media&token=9a16c8db-4f5e-474f-9ad2-95f336863834",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-11.jpg?alt=media&token=7e585b8b-feb7-4e23-abe9-8e34aa46b531"
        ]),
        new("Guốc đen sang chảnh đá 7 màu", "Guốc đen thiết kế sang chảnh với đá 7 màu lấp lánh", "GB3", 690000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-7.jpg?alt=media&token=f78cd442-b180-4dba-ab28-a932782eb12f",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-17.jpg?alt=media&token=d3d79ab1-dfbc-407b-97d6-ed680b47161a"
        ]),
        new("Giày cao gót đen quai đính đá", "Giày cao gót đen với quai đính đá tinh tế", "GB2", 650000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-8.jpg?alt=media&token=755a64ef-393a-4b16-881b-fdf9693aaa5b",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-18.jpg?alt=media&token=aec88c3c-728f-4f17-814a-abaeb8e19ca2"
        ]),
        new("Giày cao gót trắng quai chéo dây phối dây đá", "Giày cao gót trắng với thiết kế quai chéo phối đá", "GW2", 630000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-9.jpg?alt=media&token=db090e4b-be9b-4be4-9190-c11592efdaa6",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-20.jpg?alt=media&token=d41bf132-c52c-43aa-ad91-14c2ff6b9546"
        ]),
        new("Giày cao gót trắng quai chéo đính đá", "Giày cao gót trắng thanh lịch với quai chéo đính đá", "GW1", 630000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-10.jpg?alt=media&token=14e0e923-e540-4094-8b80-8080e5e68069",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-19.jpg?alt=media&token=93eb3237-209e-4a79-9693-381686d1bd13"
        ]),
        new("Giày cao gót bạc tiểu thư", "Giày cao gót bạc phong cách tiểu thư", "SP1", 580000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-21.jpg?alt=media&token=1d63cf9b-c45c-4acf-84dd-855de3206046",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-22.jpg?alt=media&token=668a60fe-e472-4836-aa4a-25dc85830ba2"
        ]),
        new("Giày cao gót vàng đồng quai tỏng", "Giày cao gót vàng đồng thiết kế độc đáo", "SP2", 550000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-23.jpg?alt=media&token=7e64206e-5daa-4d96-b9ca-d9227d6662ff",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-24.jpg?alt=media&token=ec29331b-edb9-4f9e-a36d-e7a410f13b12"
        ]),
        new("Giày cao gót đính đá", "Giày cao gót đính đá lấp lánh", "SP3", 630000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-25.jpg?alt=media&token=641a10a8-9abc-42f1-9e56-e9850e8b6d5a",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-26.jpg?alt=media&token=405296c3-4b5b-4d46-a5ac-048c8ef38610"
        ]),
        new("Giày cao quai trong (đen)", "Giày cao gót đen với quai trong suốt", "SP4", 550000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-27.jpg?alt=media&token=9d7baa12-1aeb-45da-be07-40b6fbf0cbfb",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-28.jpg?alt=media&token=91f48019-28eb-4f8f-9370-67f71705a6e7"
        ]),
        new("Dép xỏ ngón", "Dép xỏ ngón thời trang", "SP5", 180000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-31.jpg?alt=media&token=9c5e0114-deb3-4a23-96a0-1f6cef0c1cb2",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-32.jpg?alt=media&token=75a3cbe8-c2ed-4006-b1ce-f2ec44802ad7",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-33.jpg?alt=media&token=99c43e81-4e51-4e8c-a610-1d71d6d666fa",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-34.jpg?alt=media&token=6dab17f7-3fc0-4172-9be4-8959db8e3e7f"
        ]),
        new("Giày cao quai nhung", "Giày cao gót với quai nhung sang trọng", "SP6", 580000, [
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-29.jpg?alt=media&token=184e6f0c-a6a0-44e6-a8a5-b054ecb2cd59",
            "https://firebasestorage.googleapis.com/v0/b/xuthi-6f838.appspot.com/o/Juva-30.jpg?alt=media&token=59093e40-9d9b-42f4-8167-a3573082038e"
        ])
    ];
    
    private record ProductSeedData(string Name, string Description, string SkuPrefix, decimal Price, List<string> ImageUrls);
}

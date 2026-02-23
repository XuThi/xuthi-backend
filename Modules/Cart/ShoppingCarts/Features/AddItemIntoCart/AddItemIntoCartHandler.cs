using Cart.Data;
using Cart.ShoppingCarts.Models;
using ProductCatalog.Data;
using Promotion.Data;

namespace Cart.ShoppingCarts.Features.AddItemIntoCart;

public record AddToCartCommand(string? SessionId, Guid? CustomerId, Guid ProductId, Guid VariantId, int Quantity = 1) : ICommand<AddToCartResult>;
public record AddToCartResult(Guid CartId, CartDto Cart);

/// <summary>
/// Add item to cart. Creates cart if doesn't exist.
/// </summary>
internal class AddToCartHandler(CartDbContext cartDb, ProductCatalogDbContext catalogDb, PromotionDbContext promotionDb)
    : ICommandHandler<AddToCartCommand, AddToCartResult>
{
    public async Task<AddToCartResult> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        // Get or create cart
        var cart = await GetOrCreateCart(cmd.SessionId, cmd.CustomerId, ct);

        // Get variant (no Product navigation in BFF design)
        var variant = await catalogDb.Variants
            .Include(v => v.OptionSelections)
            .FirstOrDefaultAsync(v => v.Id == cmd.VariantId && !v.IsDeleted, ct);

        if (variant is null)
            throw new InvalidOperationException($"Variant {cmd.VariantId} not found");

        // Get product using variant.ProductId
        var product = await catalogDb.Products
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
            .FirstOrDefaultAsync(p => p.Id == variant.ProductId && !p.IsDeleted, ct);

        if (product is null)
            throw new InvalidOperationException($"Product for variant {cmd.VariantId} not found");

        // Build a map for option names (for display)
        var optionIds = variant.OptionSelections.Select(os => os.VariantOptionId).Distinct().ToList();
        var optionNameMap = optionIds.Count == 0
            ? new Dictionary<string, string>()
            : await catalogDb.VariantOptions
                .Where(vo => optionIds.Contains(vo.Id))
                .ToDictionaryAsync(vo => vo.Id, vo => vo.Name, ct);

        // Check if item already in cart
        var existingItem = cart.Items.FirstOrDefault(i => i.VariantId == cmd.VariantId);

        if (existingItem is not null)
        {
            // Update quantity with stock validation
            var newQty = existingItem.Quantity + cmd.Quantity;
            if (newQty > variant.StockQuantity)
                throw new InvalidOperationException(
                    $"Không đủ tồn kho. Chỉ còn {variant.StockQuantity} sản phẩm.");

            existingItem.Quantity = newQty;
            var (unitPrice, compareAtPrice) = await ResolveSalePrice(
                product.Id,
                variant.Id,
                variant.Price,
                ct);
            existingItem.UnitPrice = unitPrice;
            existingItem.CompareAtPrice = compareAtPrice;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Add new item
            var imageUrl = product.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Image.Url;

            var variantDesc = string.Join(", ", variant.OptionSelections.Select(os =>
            {
                var name = optionNameMap.TryGetValue(os.VariantOptionId, out var n) ? n : os.VariantOptionId;
                return $"{name}: {os.Value}";
            }));

            var (unitPrice, compareAtPrice) = await ResolveSalePrice(
                product.Id,
                variant.Id,
                variant.Price,
                ct);

            var newItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = product.Id,
                VariantId = cmd.VariantId,
                ProductName = product.Name,
                VariantSku = variant.Sku,
                VariantDescription = variantDesc,
                ImageUrl = imageUrl,
                UnitPrice = unitPrice,
                CompareAtPrice = compareAtPrice,
                Quantity = cmd.Quantity,
                AvailableStock = variant.StockQuantity,
                IsInStock = variant.StockQuantity > 0
            };
            cartDb.CartItems.Add(newItem);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await cartDb.SaveChangesAsync(ct);

        return new AddToCartResult(cart.Id, MapToDto(cart));
    }

    private async Task<ShoppingCart> GetOrCreateCart(string? sessionId, Guid? customerId, CancellationToken ct)
    {
        ShoppingCart? cart = null;

        if (customerId.HasValue)
        {
            cart = await cartDb.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        }
        else if (!string.IsNullOrEmpty(sessionId))
        {
            cart = await cartDb.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);
        }

        if (cart is null)
        {
            cart = new ShoppingCart
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                CustomerId = customerId,
                ExpiresAt = DateTime.UtcNow.AddDays(7) // Cart expires in 7 days
            };
            cartDb.ShoppingCarts.Add(cart);
        }

        return cart;
    }

    private static CartDto MapToDto(ShoppingCart cart) => new(
        cart.Id,
        cart.SessionId,
        cart.CustomerId,
        cart.Items.Select(i => new CartItemDto(
            i.Id, i.ProductId, i.VariantId,
            i.ProductName, i.VariantSku, i.VariantDescription, i.ImageUrl,
            i.UnitPrice, i.CompareAtPrice, i.Quantity, i.TotalPrice,
            i.AvailableStock, i.IsInStock, i.IsOnSale
        )).ToList(),
        cart.Subtotal,
        cart.VoucherDiscount,
        cart.AppliedVoucherCode,
        cart.Total,
        cart.TotalItems
    );

    private async Task<(decimal UnitPrice, decimal? CompareAtPrice)> ResolveSalePrice(
        Guid productId,
        Guid variantId,
        decimal basePrice,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var saleItem = await promotionDb.SaleCampaignItems
            .Include(i => i.SaleCampaign)
            .Where(i => i.ProductId == productId && (i.VariantId == null || i.VariantId == variantId))
            .Where(i => i.SaleCampaign.IsActive && i.SaleCampaign.StartDate <= now && i.SaleCampaign.EndDate >= now)
            .OrderByDescending(i => i.VariantId.HasValue)
            .ThenBy(i => i.SalePrice)
            .FirstOrDefaultAsync(ct);

        if (saleItem is null)
        {
            return (basePrice, null);
        }

        var original = saleItem.OriginalPrice ?? basePrice;
        if (original < saleItem.SalePrice)
        {
            original = basePrice;
        }

        return (saleItem.SalePrice, original);
    }
}

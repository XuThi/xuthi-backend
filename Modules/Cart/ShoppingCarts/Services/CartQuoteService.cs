using Customer.Customers.Features.GetCustomerPromotionFacts;
using ProductCatalog.Products.Features.GetCartItemFacts;
using Promotion.SaleCampaigns.Features.GetActiveSaleItems;
using Promotion.Vouchers.Features.ValidateVoucher;
using Promotion.Vouchers.Models;

namespace Cart.ShoppingCarts.Services;

public record CartQuote(
    Guid CartId,
    Guid? CustomerId,
    List<CartQuoteItem> Items,
    decimal Subtotal,
    decimal VoucherDiscount,
    Guid? AppliedVoucherId,
    string? AppliedVoucherCode,
    bool WaivesShipping,
    decimal Total,
    int TotalItems,
    List<string> Warnings);

public record CartQuoteItem(
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantSku,
    string? VariantDescription,
    string? ImageUrl,
    decimal UnitPrice,
    decimal? CompareAtPrice,
    int Quantity,
    decimal TotalPrice,
    int AvailableStock,
    bool IsInStock,
    bool IsOnSale);

internal class CartQuoteService(ISender sender)
{
    public async Task<CartQuote> RefreshQuoteAsync(
        ShoppingCart cart,
        bool requirePurchasable,
        bool requireVoucherValid,
        CancellationToken ct)
    {
        if (cart.Status != CartStatus.Active)
            throw new InvalidOperationException("Cart is no longer active");

        var warnings = new List<string>();

        if (cart.Items.Count == 0)
        {
            ClearVoucher(cart);
            return ToQuote(cart, warnings, waivesShipping: false);
        }

        var variantIds = cart.Items.Select(i => i.VariantId).Distinct().ToList();
        var factsResult = await sender.Send(new GetCartItemFactsQuery(variantIds), ct);
        var facts = factsResult.Items.ToDictionary(i => i.VariantId);

        var productIds = facts.Values.Select(f => f.ProductId).Distinct().ToList();
        var saleItems = await sender.Send(new GetActiveSaleItemsQuery(productIds, variantIds), ct);

        foreach (var item in cart.Items)
        {
            if (!facts.TryGetValue(item.VariantId, out var fact))
            {
                item.AvailableStock = 0;
                item.IsInStock = false;
                warnings.Add($"{item.ProductName} is no longer available");
                continue;
            }

            item.ProductId = fact.ProductId;
            item.ProductName = fact.ProductName;
            item.VariantSku = fact.VariantSku;
            item.VariantDescription = fact.VariantDescription;
            item.ImageUrl = fact.ImageUrl;
            item.AvailableStock = fact.StockQuantity;
            item.IsInStock = fact.IsAvailable && fact.StockQuantity >= item.Quantity;

            var saleItem = saleItems.Items
                .Where(i => i.ProductId == fact.ProductId && (i.VariantId == null || i.VariantId == fact.VariantId))
                .OrderByDescending(i => i.VariantId.HasValue)
                .ThenBy(i => i.SalePrice)
                .FirstOrDefault();

            if (saleItem is null)
            {
                item.UnitPrice = fact.BasePrice;
                item.CompareAtPrice = fact.CompareAtPrice;
            }
            else
            {
                var original = saleItem.OriginalPrice ?? fact.BasePrice;
                if (original < saleItem.SalePrice)
                    original = fact.BasePrice;

                item.UnitPrice = saleItem.SalePrice;
                item.CompareAtPrice = original;
            }

            item.UpdatedAt = DateTime.UtcNow;
        }

        var unavailable = cart.Items
            .Where(i => !i.IsInStock || i.AvailableStock < i.Quantity)
            .ToList();

        if (requirePurchasable && unavailable.Count > 0)
        {
            var first = unavailable[0];
            throw new InvalidOperationException(
                $"Không đủ tồn kho cho sản phẩm {first.VariantSku}. Chỉ còn {first.AvailableStock} sản phẩm.");
        }

        var waivesShipping = await RefreshVoucherAsync(cart, facts, requireVoucherValid, warnings, ct);

        return ToQuote(cart, warnings, waivesShipping);
    }

    private async Task<bool> RefreshVoucherAsync(
        ShoppingCart cart,
        Dictionary<Guid, CartItemFact> facts,
        bool requireVoucherValid,
        List<string> warnings,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cart.AppliedVoucherCode))
        {
            ClearVoucher(cart);
            return false;
        }

        var customerFacts = cart.CustomerId.HasValue
            ? (await sender.Send(new GetCustomerPromotionFactsQuery(cart.CustomerId.Value), ct)).Customer
            : null;

        var lines = cart.Items
            .Where(i => facts.ContainsKey(i.VariantId))
            .Select(i =>
            {
                var fact = facts[i.VariantId];
                return new VoucherValidationLine(
                    i.ProductId,
                    fact.CategoryId,
                    i.TotalPrice,
                    i.IsOnSale);
            })
            .ToList();

        var validateResult = await sender.Send(new ValidateVoucherQuery(
            cart.AppliedVoucherCode,
            cart.Subtotal,
            cart.Items.Select(i => i.ProductId).Distinct().ToList(),
            null,
            cart.CustomerId,
            customerFacts is null ? null : (int)customerFacts.Tier,
            customerFacts?.TotalOrders,
            lines), ct);

        if (!validateResult.IsValid)
        {
            if (requireVoucherValid)
                throw new InvalidOperationException(validateResult.ErrorMessage ?? "Invalid voucher");

            warnings.Add(validateResult.ErrorMessage ?? "Applied voucher is no longer valid");
            ClearVoucher(cart);
            return false;
        }

        cart.AppliedVoucherId = validateResult.VoucherId;
        cart.AppliedVoucherCode = cart.AppliedVoucherCode.ToUpperInvariant().Trim();
        cart.VoucherDiscount = validateResult.DiscountAmount;
        return validateResult.Type == VoucherType.FreeShipping;
    }

    private static void ClearVoucher(ShoppingCart cart)
    {
        cart.AppliedVoucherId = null;
        cart.AppliedVoucherCode = null;
        cart.VoucherDiscount = 0;
    }

    private static CartQuote ToQuote(
        ShoppingCart cart,
        List<string> warnings,
        bool waivesShipping) => new(
        cart.Id,
        cart.CustomerId,
        [.. cart.Items.Select(i => new CartQuoteItem(
            i.ProductId,
            i.VariantId,
            i.ProductName,
            i.VariantSku,
            i.VariantDescription,
            i.ImageUrl,
            i.UnitPrice,
            i.CompareAtPrice,
            i.Quantity,
            i.TotalPrice,
            i.AvailableStock,
            i.IsInStock,
            i.IsOnSale))],
        cart.Subtotal,
        cart.VoucherDiscount,
        cart.AppliedVoucherId,
        cart.AppliedVoucherCode,
        waivesShipping,
        cart.Total,
        cart.TotalItems,
        warnings);
}

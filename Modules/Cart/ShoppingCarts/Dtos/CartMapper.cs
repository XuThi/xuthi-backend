using Cart.ShoppingCarts.Models;

namespace Cart.ShoppingCarts.Dtos;

internal static class CartMapper
{
    public static CartDto ToDto(ShoppingCart cart, bool waivesShipping = false) => new(
        cart.Id,
        cart.SessionId,
        cart.CustomerId,
        [.. cart.Items.Select(i => new CartItemDto(
            i.Id,
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
            i.IsOnSale
        ))],
        cart.Subtotal,
        cart.VoucherDiscount,
        cart.AppliedVoucherCode,
        waivesShipping,
        cart.Total,
        cart.TotalItems
    );
}

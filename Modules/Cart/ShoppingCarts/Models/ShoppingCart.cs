using Core.DDD;

namespace Cart.ShoppingCarts.Models;

/// <summary>
/// Shopping cart aggregate root. Can be anonymous (SessionId) or linked to a customer.
/// Cart auto-expires after a period of inactivity.
/// </summary>
public class ShoppingCart : Aggregate<Guid>
{
    // Identity - either SessionId for anonymous or CustomerId for logged in
    public string? SessionId { get; set; } // Browser session ID (anonymous)
    public Guid? CustomerId { get; set; } // Customer ID (logged in)

    // Applied voucher (validated, not yet used until checkout)
    public Guid? AppliedVoucherId { get; set; }
    public string? AppliedVoucherCode { get; set; }
    public decimal VoucherDiscount { get; set; }

    // Auto-expire inactive carts
    public DateTime? ExpiresAt { get; set; }

    // Items
    public List<CartItem> Items { get; set; } = [];

    // Computed totals (calculated on read, not stored)
    public decimal Subtotal => Items.Sum(i => i.TotalPrice);
    public decimal Total => Math.Max(0, Subtotal - VoucherDiscount);
    public int TotalItems => Items.Sum(i => i.Quantity);
}

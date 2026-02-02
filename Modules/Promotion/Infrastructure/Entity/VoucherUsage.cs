namespace Promotion.Infrastructure.Entity;

/// <summary>
/// Track voucher usage per customer (requires Customer module).
/// For future: prevent exceeding MaxUsagePerCustomer.
/// </summary>
public class VoucherUsage
{
    public Guid Id { get; set; }
    public Guid VoucherId { get; set; }
    public Guid? CustomerId { get; set; } // Nullable for anonymous users
    public string? SessionId { get; set; } // For anonymous tracking
    public Guid OrderId { get; set; }
    public decimal DiscountApplied { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Voucher Voucher { get; set; } = null!;
}

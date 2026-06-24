using Core.DDD;

namespace Promotion.Vouchers.Models;

/// <summary>
/// Track voucher usage per customer.
/// </summary>
public class VoucherUsage : Entity<Guid>
{
    public Guid VoucherId { get; set; }
    public Guid? CustomerId { get; set; } // Nullable for anonymous users
    public string? SessionId { get; set; } // For anonymous tracking
    public Guid OrderId { get; set; }
    public decimal DiscountApplied { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    public VoucherUsageStatus Status { get; set; } = VoucherUsageStatus.Held;
    public DateTime? FinalizedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    // Navigation
    public Voucher Voucher { get; set; } = null!;
}

public enum VoucherUsageStatus
{
    Held = 1,
    Finalized = 2,
    Released = 3
}

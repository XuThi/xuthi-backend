namespace Promotion.Infrastructure.Entity;

/// <summary>
/// Voucher/Coupon for discounts. Flexible design for future expansion.
/// </summary>
public class Voucher
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!; // e.g., "SUMMER20", "XUTHI10"
    public string? Description { get; set; }
    public string? InternalNote { get; set; } // Admin notes
    
    // Discount type and value
    public VoucherType Type { get; set; }
    public decimal DiscountValue { get; set; } // Percentage (20) or fixed amount (50000)
    
    // Constraints
    public decimal? MinimumOrderAmount { get; set; } // Minimum cart value to apply
    public decimal? MaximumDiscountAmount { get; set; } // Cap for percentage discounts
    
    // Usage limits
    public int? MaxUsageCount { get; set; } // Total times this voucher can be used
    public int CurrentUsageCount { get; set; } // How many times it's been used
    public int? MaxUsagePerCustomer { get; set; } // Limit per customer (requires Customer module)
    
    // Validity period
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    // Scope restrictions (future-proof)
    public Guid? ApplicableCategoryId { get; set; } // Only for products in this category
    public List<Guid>? ApplicableProductIds { get; set; } // Only for specific products
    
    // Customer tier restriction (future)
    public int? MinimumCustomerTier { get; set; } // e.g., tier >= 2 can use this voucher
    
    // Combinability (future)
    public bool CanCombineWithOtherVouchers { get; set; } = false;
    public bool CanCombineWithSalePrice { get; set; } = true; // Apply on already-discounted items
    
    // First-time buyer only (future)
    public bool FirstPurchaseOnly { get; set; } = false;
    
    // Status
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Computed
    public bool IsValid => IsActive 
        && StartDate <= DateTime.UtcNow 
        && EndDate >= DateTime.UtcNow
        && (!MaxUsageCount.HasValue || CurrentUsageCount < MaxUsageCount);
}

public enum VoucherType
{
    Percentage = 1,     // 20% off
    FixedAmount = 2,    // 50,000 VND off
    FreeShipping = 3    // Free shipping
}

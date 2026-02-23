using Core.DDD;

namespace Promotion.SaleCampaigns.Models;

/// <summary>
/// Sale campaign types:
/// - Flash Sale: Very short (hours to 1 day)
/// - Seasonal Sale: Multi-day/week - "Black Friday", "11.11", "Winter Sale"
/// - Clearance: Until stock depletes
/// </summary>
public enum SaleCampaignType
{
    FlashSale = 1,
    SeasonalSale = 2,
    Clearance = 3,
    MemberExclusive = 4
}

/// <summary>
/// Sale campaign aggregate root for promotional pricing.
/// </summary>
public class SaleCampaign : Aggregate<Guid>
{
    public string Name { get; set; } = default!;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }

    public SaleCampaignType Type { get; set; } = SaleCampaignType.SeasonalSale;

    // Timing
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Status
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }

    // Products in this campaign
    public List<SaleCampaignItem> Items { get; set; } = [];

    // Computed
    public bool IsRunning => IsActive
        && StartDate <= DateTime.UtcNow
        && EndDate >= DateTime.UtcNow;

    public bool IsUpcoming => IsActive && StartDate > DateTime.UtcNow;
}

// Backward compatibility
public class FlashSale : SaleCampaign { }

/// <summary>
/// A product included in a sale campaign with its discounted price.
/// </summary>
public class SaleCampaignItem : Entity<Guid>
{
    public Guid SaleCampaignId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; } // Null = all variants of product

    // Sale pricing
    public decimal SalePrice { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }

    // Stock limit for this campaign (optional)
    public int? MaxQuantity { get; set; }
    public int SoldQuantity { get; set; }

    // Navigation
    public SaleCampaign SaleCampaign { get; set; } = null!;

    // Computed
    public bool HasStock => !MaxQuantity.HasValue || SoldQuantity < MaxQuantity;
}

// Backward compatibility
public class FlashSaleItem : SaleCampaignItem
{
    public Guid FlashSaleId { get => SaleCampaignId; set => SaleCampaignId = value; }
}

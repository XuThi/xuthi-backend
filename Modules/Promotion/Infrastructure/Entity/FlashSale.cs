namespace Promotion.Infrastructure.Entity;

/// <summary>
/// Sale campaign types:
/// - Flash Sale: Very short (hours to 1 day) - "Happy Hour", "Deal of the Day"
/// - Seasonal Sale: Multi-day/week - "Black Friday", "11.11", "Winter Sale"
/// - Clearance: Until stock depletes
/// </summary>
public enum SaleCampaignType
{
    FlashSale = 1,      // Hours to 1 day
    SeasonalSale = 2,   // Black Friday, 11.11, Winter Sale
    Clearance = 3,      // Until stock runs out
    MemberExclusive = 4 // For specific customer tiers
}

/// <summary>
/// Promotional sale campaigns for products.
/// Examples: "Black Friday 2025", "Winter Sale", "11.11 Mega Sale", "Flash Deal".
/// </summary>
public class SaleCampaign
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!; // "Black Friday 2025"
    public string? Slug { get; set; } // "black-friday-2025" for URLs
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; } // Campaign banner
    
    public SaleCampaignType Type { get; set; } = SaleCampaignType.SeasonalSale;
    
    // Timing
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    // Status
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; } // Show on homepage
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
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
public class SaleCampaignItem
{
    public Guid Id { get; set; }
    public Guid SaleCampaignId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; } // Null = all variants of product
    
    // Sale pricing
    public decimal SalePrice { get; set; }
    public decimal? OriginalPrice { get; set; } // For strikethrough display
    public decimal? DiscountPercentage { get; set; } // Calculated or manual
    
    // Stock limit for this campaign (optional)
    public int? MaxQuantity { get; set; } // Limited stock deal
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

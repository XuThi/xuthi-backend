using Promotion.Infrastructure.Entity;

namespace Promotion.Features.SaleCampaigns;

// ========== CREATE ==========
public record CreateSaleCampaignCommand(CreateSaleCampaignRequest Request) 
    : ICommand<SaleCampaignResult>;

public record CreateSaleCampaignRequest(
    string Name,
    string? Description,
    string? BannerImageUrl,
    SaleCampaignType Type,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive = true,
    bool IsFeatured = false,
    List<CreateSaleCampaignItemRequest>? Items = null
);

public record CreateSaleCampaignItemRequest(
    Guid ProductId,
    Guid? VariantId,
    decimal SalePrice,
    decimal? OriginalPrice,
    decimal? DiscountPercentage,
    int? MaxQuantity
);

// ========== UPDATE ==========
public record UpdateSaleCampaignCommand(Guid Id, UpdateSaleCampaignRequest Request) 
    : ICommand<SaleCampaignResult>;

public record UpdateSaleCampaignRequest(
    string? Name = null,
    string? Description = null,
    string? BannerImageUrl = null,
    SaleCampaignType? Type = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    bool? IsActive = null,
    bool? IsFeatured = null
);

// ========== DELETE ==========
public record DeleteSaleCampaignCommand(Guid Id) : ICommand<bool>;

// ========== ITEM OPERATIONS ==========
public record AddSaleCampaignItemCommand(Guid CampaignId, CreateSaleCampaignItemRequest Item) 
    : ICommand<SaleCampaignItemResult>;

public record UpdateSaleCampaignItemCommand(Guid ItemId, UpdateSaleCampaignItemRequest Request) 
    : ICommand<SaleCampaignItemResult>;

public record UpdateSaleCampaignItemRequest(
    decimal? SalePrice = null,
    decimal? OriginalPrice = null,
    decimal? DiscountPercentage = null,
    int? MaxQuantity = null
);

public record RemoveSaleCampaignItemCommand(Guid ItemId) : ICommand<bool>;

// ========== QUERIES ==========
public record GetSaleCampaignQuery(Guid Id) : IQuery<SaleCampaignDetailResult>;

public record GetSaleCampaignsQuery(
    bool? IsActive = null,
    bool? IsFeatured = null,
    SaleCampaignType? Type = null,
    bool? OnlyRunning = null,
    bool? OnlyUpcoming = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<SaleCampaignsResult>;

public record GetSaleCampaignBySlugQuery(string Slug) : IQuery<SaleCampaignDetailResult>;

// ========== RESULTS ==========
public record SaleCampaignResult(
    Guid Id,
    string Name,
    string? Slug,
    string? Description,
    string? BannerImageUrl,
    SaleCampaignType Type,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    bool IsFeatured,
    bool IsRunning,
    bool IsUpcoming,
    int ItemCount
);

public record SaleCampaignDetailResult(
    Guid Id,
    string Name,
    string? Slug,
    string? Description,
    string? BannerImageUrl,
    SaleCampaignType Type,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    bool IsFeatured,
    bool IsRunning,
    bool IsUpcoming,
    List<SaleCampaignItemResult> Items
);

public record SaleCampaignItemResult(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    decimal SalePrice,
    decimal? OriginalPrice,
    decimal? DiscountPercentage,
    int? MaxQuantity,
    int SoldQuantity,
    bool HasStock
);

public record SaleCampaignsResult(
    List<SaleCampaignResult> Items,
    int TotalCount,
    int Page,
    int PageSize
);

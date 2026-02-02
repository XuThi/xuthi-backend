using Microsoft.EntityFrameworkCore;
using Promotion.Infrastructure.Data;
using Promotion.Infrastructure.Entity;

namespace Promotion.Features.SaleCampaigns;

// ========== CREATE HANDLER ==========
internal class CreateSaleCampaignHandler(PromotionDbContext dbContext)
    : ICommandHandler<CreateSaleCampaignCommand, SaleCampaignResult>
{
    public async Task<SaleCampaignResult> Handle(CreateSaleCampaignCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;
        
        var campaign = new SaleCampaign
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Slug = GenerateSlug(req.Name),
            Description = req.Description,
            BannerImageUrl = req.BannerImageUrl,
            Type = req.Type,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            IsActive = req.IsActive,
            IsFeatured = req.IsFeatured,
            CreatedAt = DateTime.UtcNow
        };

        // Add items if provided
        if (req.Items?.Count > 0)
        {
            foreach (var item in req.Items)
            {
                campaign.Items.Add(new SaleCampaignItem
                {
                    Id = Guid.NewGuid(),
                    SaleCampaignId = campaign.Id,
                    ProductId = item.ProductId,
                    VariantId = item.VariantId,
                    SalePrice = item.SalePrice,
                    OriginalPrice = item.OriginalPrice,
                    DiscountPercentage = item.DiscountPercentage,
                    MaxQuantity = item.MaxQuantity,
                    SoldQuantity = 0
                });
            }
        }

        dbContext.SaleCampaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(campaign);
    }

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "");
    
    private static SaleCampaignResult MapToResult(SaleCampaign c) => new(
        c.Id, c.Name, c.Slug, c.Description, c.BannerImageUrl,
        c.Type, c.StartDate, c.EndDate, c.IsActive, c.IsFeatured,
        c.IsRunning, c.IsUpcoming, c.Items.Count
    );
}

// ========== UPDATE HANDLER ==========
internal class UpdateSaleCampaignHandler(PromotionDbContext dbContext)
    : ICommandHandler<UpdateSaleCampaignCommand, SaleCampaignResult>
{
    public async Task<SaleCampaignResult> Handle(UpdateSaleCampaignCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
            
        if (campaign is null)
            throw new KeyNotFoundException("Sale campaign not found");

        var req = command.Request;
        
        if (req.Name != null)
        {
            campaign.Name = req.Name;
            campaign.Slug = GenerateSlug(req.Name);
        }
        if (req.Description != null) campaign.Description = req.Description;
        if (req.BannerImageUrl != null) campaign.BannerImageUrl = req.BannerImageUrl;
        if (req.Type.HasValue) campaign.Type = req.Type.Value;
        if (req.StartDate.HasValue) campaign.StartDate = req.StartDate.Value;
        if (req.EndDate.HasValue) campaign.EndDate = req.EndDate.Value;
        if (req.IsActive.HasValue) campaign.IsActive = req.IsActive.Value;
        if (req.IsFeatured.HasValue) campaign.IsFeatured = req.IsFeatured.Value;
        
        campaign.UpdatedAt = DateTime.UtcNow;
        
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SaleCampaignResult(
            campaign.Id, campaign.Name, campaign.Slug, campaign.Description, 
            campaign.BannerImageUrl, campaign.Type, campaign.StartDate, campaign.EndDate,
            campaign.IsActive, campaign.IsFeatured, campaign.IsRunning, campaign.IsUpcoming,
            campaign.Items.Count
        );
    }

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(" ", "-").Replace(".", "").Replace(",", "");
}

// ========== DELETE HANDLER ==========
internal class DeleteSaleCampaignHandler(PromotionDbContext dbContext)
    : ICommandHandler<DeleteSaleCampaignCommand, bool>
{
    public async Task<bool> Handle(DeleteSaleCampaignCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
            
        if (campaign is null)
            return false;

        // Remove all items first
        dbContext.SaleCampaignItems.RemoveRange(campaign.Items);
        dbContext.SaleCampaigns.Remove(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}

// ========== GET CAMPAIGN HANDLER ==========
internal class GetSaleCampaignHandler(PromotionDbContext dbContext)
    : IQueryHandler<GetSaleCampaignQuery, SaleCampaignDetailResult>
{
    public async Task<SaleCampaignDetailResult> Handle(GetSaleCampaignQuery query, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
            
        if (campaign is null)
            throw new KeyNotFoundException("Sale campaign not found");

        return MapToDetailResult(campaign);
    }

    private static SaleCampaignDetailResult MapToDetailResult(SaleCampaign c) => new(
        c.Id, c.Name, c.Slug, c.Description, c.BannerImageUrl,
        c.Type, c.StartDate, c.EndDate, c.IsActive, c.IsFeatured,
        c.IsRunning, c.IsUpcoming,
        c.Items.Select(i => new SaleCampaignItemResult(
            i.Id, i.ProductId, i.VariantId, i.SalePrice, i.OriginalPrice,
            i.DiscountPercentage, i.MaxQuantity, i.SoldQuantity, i.HasStock
        )).ToList()
    );
}

// ========== GET BY SLUG HANDLER ==========
internal class GetSaleCampaignBySlugHandler(PromotionDbContext dbContext)
    : IQueryHandler<GetSaleCampaignBySlugQuery, SaleCampaignDetailResult>
{
    public async Task<SaleCampaignDetailResult> Handle(GetSaleCampaignBySlugQuery query, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Slug == query.Slug, cancellationToken);
            
        if (campaign is null)
            throw new KeyNotFoundException("Sale campaign not found");

        return new SaleCampaignDetailResult(
            campaign.Id, campaign.Name, campaign.Slug, campaign.Description, campaign.BannerImageUrl,
            campaign.Type, campaign.StartDate, campaign.EndDate, campaign.IsActive, campaign.IsFeatured,
            campaign.IsRunning, campaign.IsUpcoming,
            campaign.Items.Select(i => new SaleCampaignItemResult(
                i.Id, i.ProductId, i.VariantId, i.SalePrice, i.OriginalPrice,
                i.DiscountPercentage, i.MaxQuantity, i.SoldQuantity, i.HasStock
            )).ToList()
        );
    }
}

// ========== GET CAMPAIGNS LIST HANDLER ==========
internal class GetSaleCampaignsHandler(PromotionDbContext dbContext)
    : IQueryHandler<GetSaleCampaignsQuery, SaleCampaignsResult>
{
    public async Task<SaleCampaignsResult> Handle(GetSaleCampaignsQuery query, CancellationToken cancellationToken)
    {
        var q = dbContext.SaleCampaigns.Include(c => c.Items).AsQueryable();

        if (query.IsActive.HasValue)
            q = q.Where(c => c.IsActive == query.IsActive.Value);
            
        if (query.IsFeatured.HasValue)
            q = q.Where(c => c.IsFeatured == query.IsFeatured.Value);
            
        if (query.Type.HasValue)
            q = q.Where(c => c.Type == query.Type.Value);

        var now = DateTime.UtcNow;
        if (query.OnlyRunning == true)
            q = q.Where(c => c.IsActive && c.StartDate <= now && c.EndDate >= now);
            
        if (query.OnlyUpcoming == true)
            q = q.Where(c => c.IsActive && c.StartDate > now);

        var totalCount = await q.CountAsync(cancellationToken);
        
        var items = await q
            .OrderByDescending(c => c.IsFeatured)
            .ThenByDescending(c => c.StartDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new SaleCampaignsResult(
            items.Select(c => new SaleCampaignResult(
                c.Id, c.Name, c.Slug, c.Description, c.BannerImageUrl,
                c.Type, c.StartDate, c.EndDate, c.IsActive, c.IsFeatured,
                c.IsRunning, c.IsUpcoming, c.Items.Count
            )).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );
    }
}

// ========== ITEM HANDLERS ==========
internal class AddSaleCampaignItemHandler(PromotionDbContext dbContext)
    : ICommandHandler<AddSaleCampaignItemCommand, SaleCampaignItemResult>
{
    public async Task<SaleCampaignItemResult> Handle(AddSaleCampaignItemCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns.FindAsync([command.CampaignId], cancellationToken);
        if (campaign is null)
            throw new KeyNotFoundException("Sale campaign not found");

        var item = new SaleCampaignItem
        {
            Id = Guid.NewGuid(),
            SaleCampaignId = command.CampaignId,
            ProductId = command.Item.ProductId,
            VariantId = command.Item.VariantId,
            SalePrice = command.Item.SalePrice,
            OriginalPrice = command.Item.OriginalPrice,
            DiscountPercentage = command.Item.DiscountPercentage,
            MaxQuantity = command.Item.MaxQuantity,
            SoldQuantity = 0
        };

        dbContext.SaleCampaignItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SaleCampaignItemResult(
            item.Id, item.ProductId, item.VariantId, item.SalePrice,
            item.OriginalPrice, item.DiscountPercentage, item.MaxQuantity, 
            item.SoldQuantity, item.HasStock
        );
    }
}

internal class UpdateSaleCampaignItemHandler(PromotionDbContext dbContext)
    : ICommandHandler<UpdateSaleCampaignItemCommand, SaleCampaignItemResult>
{
    public async Task<SaleCampaignItemResult> Handle(UpdateSaleCampaignItemCommand command, CancellationToken cancellationToken)
    {
        var item = await dbContext.SaleCampaignItems.FindAsync([command.ItemId], cancellationToken);
        if (item is null)
            throw new KeyNotFoundException("Sale campaign item not found");

        var req = command.Request;
        if (req.SalePrice.HasValue) item.SalePrice = req.SalePrice.Value;
        if (req.OriginalPrice.HasValue) item.OriginalPrice = req.OriginalPrice.Value;
        if (req.DiscountPercentage.HasValue) item.DiscountPercentage = req.DiscountPercentage.Value;
        if (req.MaxQuantity.HasValue) item.MaxQuantity = req.MaxQuantity.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SaleCampaignItemResult(
            item.Id, item.ProductId, item.VariantId, item.SalePrice,
            item.OriginalPrice, item.DiscountPercentage, item.MaxQuantity, 
            item.SoldQuantity, item.HasStock
        );
    }
}

internal class RemoveSaleCampaignItemHandler(PromotionDbContext dbContext)
    : ICommandHandler<RemoveSaleCampaignItemCommand, bool>
{
    public async Task<bool> Handle(RemoveSaleCampaignItemCommand command, CancellationToken cancellationToken)
    {
        var item = await dbContext.SaleCampaignItems.FindAsync([command.ItemId], cancellationToken);
        if (item is null)
            return false;

        dbContext.SaleCampaignItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

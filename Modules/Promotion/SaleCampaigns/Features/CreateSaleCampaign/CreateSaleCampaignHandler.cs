using Promotion.SaleCampaigns.Events;

namespace Promotion.SaleCampaigns.Features.CreateSaleCampaign;

public record CreateSaleCampaignCommand(CreateSaleCampaignRequest Request) : ICommand<SaleCampaignResult>;

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

        if (req.Items?.Count > 0)
        {
            EnsureNoLocalConflicts(req.Items);
            await EnsureNoActiveOverlaps(req, cancellationToken);

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

        // Raise domain event for subscriber notification (only if requested)
        if (req.NotifySubscribers)
        {
            campaign.AddDomainEvent(new SaleCampaignCreatedEvent(
                campaign.Id, campaign.Name, campaign.Slug, campaign.BannerImageUrl,
                campaign.Type, campaign.StartDate, campaign.EndDate, campaign.Items.Count));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(campaign);
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.Replace("đ", "d").Replace("Đ", "d")
            .Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in slug)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        slug = sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');
        return slug;
    }

    private static void EnsureNoLocalConflicts(List<CreateSaleCampaignItemRequest> items)
    {
        var conflicts = items
            .GroupBy(i => new { i.ProductId, Variant = i.VariantId })
            .Any(g => g.Count() > 1);

        if (conflicts)
            throw new InvalidOperationException("Sản phẩm bị trùng trong danh sách campaign.");

        var productLevel = items
            .Where(i => i.VariantId == null)
            .Select(i => i.ProductId)
            .ToHashSet();

        if (items.Any(i => i.VariantId != null && productLevel.Contains(i.ProductId)))
            throw new InvalidOperationException("Không thể mix sản phẩm toàn bộ và theo biến thể trong cùng campaign.");
    }

    private async Task EnsureNoActiveOverlaps(CreateSaleCampaignRequest req, CancellationToken ct)
    {
        if (!req.IsActive)
            return;

        var productIds = req.Items?.Select(i => i.ProductId).Distinct().ToList() ?? [];
        if (productIds.Count == 0)
            return;

        var variantIds = req.Items?.Where(i => i.VariantId.HasValue).Select(i => i.VariantId!.Value).Distinct().ToList() ?? [];

        var overlaps = await dbContext.SaleCampaignItems
            .Include(i => i.SaleCampaign)
            .Where(i => productIds.Contains(i.ProductId))
            .Where(i => i.SaleCampaign.IsActive)
            .Where(i => i.SaleCampaign.StartDate <= req.EndDate && i.SaleCampaign.EndDate >= req.StartDate)
            .Where(i => i.VariantId == null || variantIds.Contains(i.VariantId.Value) || variantIds.Count == 0)
            .AnyAsync(ct);

        if (overlaps)
            throw new InvalidOperationException("Có sản phẩm đã nằm trong campaign khác đang hoạt động.");
    }

    private static SaleCampaignResult MapToResult(SaleCampaign c) => new(
        c.Id, c.Name, c.Slug, c.Description, c.BannerImageUrl,
        c.Type, c.StartDate, c.EndDate, c.IsActive, c.IsFeatured,
        c.IsRunning, c.IsUpcoming, c.Items.Count
    );
}

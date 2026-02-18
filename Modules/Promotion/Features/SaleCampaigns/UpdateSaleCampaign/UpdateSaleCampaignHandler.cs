namespace Promotion.Features.SaleCampaigns.UpdateSaleCampaign;

public record UpdateSaleCampaignCommand(Guid Id, UpdateSaleCampaignRequest Request) : ICommand<SaleCampaignResult>;

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

        if (campaign.IsActive)
        {
            await EnsureNoActiveOverlaps(campaign, cancellationToken);
        }

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

    private async Task EnsureNoActiveOverlaps(SaleCampaign campaign, CancellationToken ct)
    {
        if (campaign.Items.Count == 0)
            return;

        var productIds = campaign.Items.Select(i => i.ProductId).Distinct().ToList();
        var variantIds = campaign.Items
            .Where(i => i.VariantId.HasValue)
            .Select(i => i.VariantId!.Value)
            .Distinct()
            .ToList();

        var overlaps = await dbContext.SaleCampaignItems
            .Include(i => i.SaleCampaign)
            .Where(i => i.SaleCampaignId != campaign.Id)
            .Where(i => productIds.Contains(i.ProductId))
            .Where(i => i.SaleCampaign.IsActive)
            .Where(i => i.SaleCampaign.StartDate <= campaign.EndDate)
            .Where(i => i.VariantId == null || variantIds.Contains(i.VariantId.Value) || variantIds.Count == 0)
            .AnyAsync(ct);

        if (overlaps)
            throw new InvalidOperationException("Campaign bị trùng sản phẩm với campaign khác đang hoạt động.");
    }
}

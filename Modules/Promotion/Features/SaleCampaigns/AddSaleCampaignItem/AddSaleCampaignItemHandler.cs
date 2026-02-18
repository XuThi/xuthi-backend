using Promotion.Infrastructure.Data;
using Promotion.Infrastructure.Entity;

namespace Promotion.Features.SaleCampaigns.AddSaleCampaignItem;

public record AddSaleCampaignItemCommand(Guid CampaignId, CreateSaleCampaignItemRequest Item) : ICommand<SaleCampaignItemResult>;

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

internal class AddSaleCampaignItemHandler(PromotionDbContext dbContext)
    : ICommandHandler<AddSaleCampaignItemCommand, SaleCampaignItemResult>
{
    public async Task<SaleCampaignItemResult> Handle(AddSaleCampaignItemCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == command.CampaignId, cancellationToken);
        if (campaign is null)
            throw new KeyNotFoundException("Sale campaign not found");

        EnsureNoLocalConflict(campaign, command.Item.ProductId, command.Item.VariantId);
        await EnsureNoActiveOverlap(campaign, command.Item.ProductId, command.Item.VariantId, cancellationToken);

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

    private static void EnsureNoLocalConflict(SaleCampaign campaign, Guid productId, Guid? variantId)
    {
        var conflict = campaign.Items.Any(i =>
            i.ProductId == productId &&
            (i.VariantId == null || variantId == null || i.VariantId == variantId));

        if (conflict)
            throw new InvalidOperationException("Sản phẩm đã tồn tại trong campaign này.");
    }

    private async Task EnsureNoActiveOverlap(SaleCampaign campaign, Guid productId, Guid? variantId, CancellationToken ct)
    {
        if (!campaign.IsActive)
            return;

        var overlapExists = await dbContext.SaleCampaignItems
            .Include(i => i.SaleCampaign)
            .Where(i => i.SaleCampaignId != campaign.Id)
            .Where(i => i.ProductId == productId)
            .Where(i => i.VariantId == null || variantId == null || i.VariantId == variantId)
            .Where(i => i.SaleCampaign.IsActive)
            .Where(i => i.SaleCampaign.StartDate <= campaign.EndDate)
            .AnyAsync(ct);

        if (overlapExists)
            throw new InvalidOperationException("Sản phẩm đã nằm trong campaign khác đang hoạt động.");
    }
}

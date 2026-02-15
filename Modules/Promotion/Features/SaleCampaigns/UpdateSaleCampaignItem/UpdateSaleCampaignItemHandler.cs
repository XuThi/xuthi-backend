namespace Promotion.Features.SaleCampaigns.UpdateSaleCampaignItem;

public record UpdateSaleCampaignItemCommand(Guid ItemId, UpdateSaleCampaignItemRequest Request) : ICommand<SaleCampaignItemResult>;

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

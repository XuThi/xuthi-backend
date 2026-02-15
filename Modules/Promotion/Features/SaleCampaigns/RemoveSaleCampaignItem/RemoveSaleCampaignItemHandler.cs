namespace Promotion.Features.SaleCampaigns.RemoveSaleCampaignItem;

public record RemoveSaleCampaignItemCommand(Guid ItemId) : ICommand<bool>;

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

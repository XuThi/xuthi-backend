using Core.Caching;
namespace Promotion.SaleCampaigns.Features.DeleteSaleCampaign;

public record DeleteSaleCampaignCommand(Guid Id) : ICommand<bool>;

internal class DeleteSaleCampaignHandler(PromotionDbContext dbContext, ICacheInvalidator cacheInvalidator)
    : ICommandHandler<DeleteSaleCampaignCommand, bool>
{
    public async Task<bool> Handle(DeleteSaleCampaignCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (campaign is null)
            return false;

        dbContext.SaleCampaignItems.RemoveRange(campaign.Items);
        dbContext.SaleCampaigns.Remove(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);
        cacheInvalidator.Invalidate(CacheKeys.SaleCampaigns, CacheKeys.ActiveSaleItems);

        return true;
    }
}

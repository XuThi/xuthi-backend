using Promotion.Infrastructure.Data;

namespace Promotion.Features.SaleCampaigns.DeleteSaleCampaign;

public record DeleteSaleCampaignCommand(Guid Id) : ICommand<bool>;

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

        dbContext.SaleCampaignItems.RemoveRange(campaign.Items);
        dbContext.SaleCampaigns.Remove(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}

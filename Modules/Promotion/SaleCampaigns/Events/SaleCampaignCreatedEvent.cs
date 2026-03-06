using Core.DDD;
using Promotion.SaleCampaigns.Models;

namespace Promotion.SaleCampaigns.Events;

public record SaleCampaignCreatedEvent(
    Guid CampaignId,
    string CampaignName,
    string? Slug,
    string? BannerImageUrl,
    SaleCampaignType Type,
    DateTime StartDate,
    DateTime EndDate,
    int ItemCount) : IDomainEvent;

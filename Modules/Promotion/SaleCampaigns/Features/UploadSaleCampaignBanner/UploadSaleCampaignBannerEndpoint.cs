using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Products.Features.Media;
using Promotion.Data;

namespace Promotion.SaleCampaigns.Features.UploadSaleCampaignBanner;

public record UploadBannerResponse(string BannerImageUrl);

public class UploadSaleCampaignBannerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sale-campaigns/{id:guid}/banner", async (
            Guid id,
            [FromForm(Name = "image")] IFormFile image,
            PromotionDbContext dbContext,
            ICloudinaryMediaService cloudinary,
            CancellationToken ct) =>
        {
            var campaign = await dbContext.SaleCampaigns.FindAsync([id], ct);
            if (campaign is null)
                return Results.NotFound("Sale campaign not found");

            // Delete old banner if exists
            if (!string.IsNullOrEmpty(campaign.BannerImagePublicId))
            {
                await cloudinary.DeleteImageAsync(campaign.BannerImagePublicId, ct);
            }

            var (url, publicId) = await cloudinary.UploadImageAsync(image, "sale-campaigns", ct);

            campaign.BannerImageUrl = url;
            campaign.BannerImagePublicId = publicId;
            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(new UploadBannerResponse(url));
        })
        .WithName("UploadSaleCampaignBanner")
        .Produces<UploadBannerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Upload Sale Campaign Banner")
        .WithDescription("Upload a banner image for a sale campaign (replaces existing)")
        .WithTags("Sale Campaigns")
        .DisableAntiforgery()
        .RequireAuthorization("Admin");
    }
}

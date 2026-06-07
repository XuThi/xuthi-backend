using System;
using System.Security.Claims;
using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ProductCatalog.Products.Features.SubmitReview;

public record SubmitReviewRequest(
    string AuthorName,
    string AuthorEmail,
    int Rating,
    string? Comment
);

public record SubmitReviewResponse(Guid ReviewId, decimal NewAverageRating, int NewReviewCount);

public class SubmitReviewEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products/{id:guid}/reviews", async (
            Guid id,
            SubmitReviewRequest body,
            ClaimsPrincipal principal,
            ISender sender) =>
        {
            var externalUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? customerId = null;
            if (!string.IsNullOrEmpty(externalUserId))
            {
                customerId = await sender.Send(new Contracts.GetCustomerByExternalIdQuery(externalUserId));
            }

            var cmd = new SubmitReviewCommand(
                ProductId: id,
                AuthorName: body.AuthorName,
                AuthorEmail: body.AuthorEmail,
                Rating: body.Rating,
                Comment: body.Comment,
                CustomerId: customerId
            );
            var result = await sender.Send(cmd);
            var response = result.Adapt<SubmitReviewResponse>();
            return Results.Created($"/api/products/{id}/reviews", response);
        })
        .RequireAuthorization()
        .WithName("SubmitProductReview")
        .Produces<SubmitReviewResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .WithSummary("Submit a review for a product (Verified Buyers only)")
        .WithTags("Products");
    }
}

using System.Security.Claims;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ProductCatalog.Products.Features.Reviews;

// TODO: Separate this into multiple endpoints later (GetReviews, SubmitReview, GetRecommendations) - for better organization and maintainability
public class ReviewEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /api/products/{id}/reviews
        app.MapGet("/api/products/{id:guid}/reviews", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetReviewsQuery(id));
            return Results.Ok(result);
        })
        .WithName("GetProductReviews")
        .Produces<GetReviewsResult>(StatusCodes.Status200OK)
        .WithSummary("Get reviews for a product")
        .WithTags("Products");

        // POST /api/products/{id}/reviews (Secure & Authenticated)
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
            return Results.Created($"/api/products/{id}/reviews", result);
        })
        .RequireAuthorization() // Enforce authentication for review submission
        .WithName("SubmitProductReview")
        .Produces<SubmitReviewResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .WithSummary("Submit a review for a product (Verified Buyers only)")
        .WithTags("Products");

        // GET /api/products/{id}/recommendations
        app.MapGet("/api/products/{id:guid}/recommendations", async (
            Guid id,
            ISender sender,
            int limit = 4) =>
        {
            var result = await sender.Send(new GetRecommendationsQuery(id, limit));
            return Results.Ok(result);
        })
        .WithName("GetProductRecommendations")
        .Produces<GetRecommendationsResult>(StatusCodes.Status200OK)
        .WithSummary("Get recommended products based on same category")
        .WithTags("Products");
    }
}

public record SubmitReviewRequest(
    string AuthorName,
    string AuthorEmail,
    int Rating,
    string? Comment
);

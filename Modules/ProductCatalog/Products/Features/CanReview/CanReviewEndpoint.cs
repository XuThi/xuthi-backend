using System.Security.Claims;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ProductCatalog.Products.Features.CanReview;

public class CanReviewEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /api/products/{id}/can-review
        app.MapGet("/api/products/{id:guid}/can-review", async (
            Guid id,
            ClaimsPrincipal principal,
            ISender sender) =>
        {
            var result = await sender.Send(new CanReviewQuery(id, principal));
            return Results.Ok(result);
        })
        .WithName("CanCustomerReviewProduct")
        .Produces<CanReviewResult>(StatusCodes.Status200OK)
        .WithSummary("Check if current logged in customer can review a product")
        .WithTags("Products");
    }
}

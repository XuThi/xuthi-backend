using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;

namespace ProductCatalog.Products.Features.GetReviews;

public record GetReviewsResponse(
    List<ProductReviewDto> Reviews,
    decimal AverageRating,
    int ReviewCount
);

public class GetReviewsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id:guid}/reviews", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetReviewsQuery(id));
            var response = result.Adapt<GetReviewsResponse>();
            return Results.Ok(response);
        })
        .WithName("GetProductReviews")
        .Produces<GetReviewsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get reviews for a product")
        .WithTags("Products");
    }
}

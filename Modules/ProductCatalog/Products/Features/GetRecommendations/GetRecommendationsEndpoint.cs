using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;

namespace ProductCatalog.Products.Features.GetRecommendations;

public record GetRecommendationsResponse(List<RecommendedProductDto> Products);

public class GetRecommendationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id:guid}/recommendations", async (
            Guid id,
            ISender sender,
            int limit = 4) =>
        {
            var result = await sender.Send(new GetRecommendationsQuery(id, limit));
            var response = result.Adapt<GetRecommendationsResponse>();
            return Results.Ok(response);
        })
        .WithName("GetProductRecommendations")
        .Produces<GetRecommendationsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get recommended products based on style, price similarity, or co-purchases")
        .WithTags("Products");
    }
}

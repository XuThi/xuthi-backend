using System.Security.Claims;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ProductCatalog.Products.Features.GetUnratedDeliveredProducts;

public class GetUnratedDeliveredProductsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /api/orders/unrated-delivered-products
        app.MapGet("/api/orders/unrated-delivered-products", async (
            ClaimsPrincipal principal,
            ISender sender) =>
        {
            var result = await sender.Send(new GetUnratedDeliveredProductsQuery(principal));
            return Results.Ok(result.Products);
        })
        .WithName("GetUnratedDeliveredProducts")
        .RequireAuthorization()
        .Produces<List<UnratedProductDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .WithSummary("Get all delivered products that the logged in customer has not yet reviewed")
        .WithTags("Orders");
    }
}

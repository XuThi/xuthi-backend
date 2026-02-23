namespace ProductCatalog.Brands.Features.GetBrandById;

public record GetBrandByIdRequest(Guid Id);
public record GetBrandByIdResponse(BrandDetailResult Brand);

public class GetBrandByIdEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/brands/{id:guid}", async (
			[AsParameters] GetBrandByIdRequest request,
			ISender sender) =>
		{
			var result = await sender.Send(new GetBrandByIdQuery(request.Id));
			var response = new GetBrandByIdResponse(result);
			return Results.Ok(response);
		})
		.WithName("GetBrandById")
		.Produces<GetBrandByIdResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get Brand by ID")
		.WithDescription("Get brand details by ID")
		.WithTags("Brands");
	}
}

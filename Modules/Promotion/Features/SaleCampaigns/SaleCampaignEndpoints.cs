using Promotion.Infrastructure.Entity;

namespace Promotion.Features.SaleCampaigns;

public class SaleCampaignEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sale-campaigns").WithTags("Sale Campaigns");

        // ========== QUERIES ==========
        group.MapGet("/", async (
            [AsParameters] GetSaleCampaignsQuery query,
            ISender sender) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetSaleCampaigns")
        .Produces<SaleCampaignsResult>(StatusCodes.Status200OK)
        .WithSummary("Get Sale Campaigns")
        .WithDescription("Get paginated list of sale campaigns with optional filters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSaleCampaignQuery(id));
            return Results.Ok(result);
        })
        .WithName("GetSaleCampaign")
        .Produces<SaleCampaignDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Sale Campaign")
        .WithDescription("Get sale campaign by ID with all items");

        group.MapGet("/by-slug/{slug}", async (string slug, ISender sender) =>
        {
            var result = await sender.Send(new GetSaleCampaignBySlugQuery(slug));
            return Results.Ok(result);
        })
        .WithName("GetSaleCampaignBySlug")
        .Produces<SaleCampaignDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Sale Campaign by Slug")
        .WithDescription("Get sale campaign by URL slug");

        // ========== COMMANDS ==========
        group.MapPost("/", async (CreateSaleCampaignRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreateSaleCampaignCommand(request));
            return Results.Created($"/api/sale-campaigns/{result.Id}", result);
        })
        .WithName("CreateSaleCampaign")
        .Produces<SaleCampaignResult>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .WithSummary("Create Sale Campaign")
        .WithDescription("Create a new sale campaign (flash sale, seasonal sale, clearance, etc.)")
        .RequireAuthorization("Admin");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSaleCampaignRequest request, ISender sender) =>
        {
            var result = await sender.Send(new UpdateSaleCampaignCommand(id, request));
            return Results.Ok(result);
        })
        .WithName("UpdateSaleCampaign")
        .Produces<SaleCampaignResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Sale Campaign")
        .WithDescription("Update sale campaign details")
        .RequireAuthorization("Admin");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var success = await sender.Send(new DeleteSaleCampaignCommand(id));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSaleCampaign")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Sale Campaign")
        .WithDescription("Delete sale campaign and all its items")
        .RequireAuthorization("Admin");

        // ========== ITEM OPERATIONS ==========
        group.MapPost("/{campaignId:guid}/items", async (
            Guid campaignId, 
            CreateSaleCampaignItemRequest request, 
            ISender sender) =>
        {
            var result = await sender.Send(new AddSaleCampaignItemCommand(campaignId, request));
            return Results.Created($"/api/sale-campaigns/{campaignId}/items/{result.Id}", result);
        })
        .WithName("AddSaleCampaignItem")
        .Produces<SaleCampaignItemResult>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .WithSummary("Add Item to Sale Campaign")
        .WithDescription("Add a product/variant to a sale campaign with discount pricing")
        .RequireAuthorization("Admin");

        group.MapPut("/items/{itemId:guid}", async (
            Guid itemId, 
            UpdateSaleCampaignItemRequest request, 
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateSaleCampaignItemCommand(itemId, request));
            return Results.Ok(result);
        })
        .WithName("UpdateSaleCampaignItem")
        .Produces<SaleCampaignItemResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Sale Campaign Item")
        .WithDescription("Update sale item pricing and stock limits")
        .RequireAuthorization("Admin");

        group.MapDelete("/items/{itemId:guid}", async (Guid itemId, ISender sender) =>
        {
            var success = await sender.Send(new RemoveSaleCampaignItemCommand(itemId));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("RemoveSaleCampaignItem")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove Item from Sale Campaign")
        .WithDescription("Remove a product from a sale campaign")
        .RequireAuthorization("Admin");
    }
}

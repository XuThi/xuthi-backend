namespace Promotion.Features.Vouchers;

public class VoucherEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vouchers")
            .WithTags("Vouchers");

        // GET /api/vouchers - Staff only (for admin panel)
        group.MapGet("/", async (
            bool? isActive,
            bool? validOnly,
            ISender sender) =>
        {
            var result = await sender.Send(new GetVouchersQuery(isActive, validOnly));
            return Results.Ok(result.Vouchers);
        })
        .WithSummary("Get all vouchers")
        .WithDescription("Filter by isActive and validOnly")
        .RequireAuthorization("Staff");

        // GET /api/vouchers/{id} - Staff only
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetVoucherQuery(id));
            return result.Voucher is null 
                ? Results.NotFound() 
                : Results.Ok(result.Voucher);
        })
        .WithSummary("Get voucher by ID")
        .RequireAuthorization("Staff");

        // POST /api/vouchers - Admin only
        group.MapPost("/", async (CreateVoucherRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreateVoucherCommand(request));
            return Results.Created($"/api/vouchers/{result.Id}", result);
        })
        .WithSummary("Create new voucher")
        .RequireAuthorization("Admin");

        // PUT /api/vouchers/{id} - Admin only
        group.MapPut("/{id:guid}", async (Guid id, UpdateVoucherRequest request, ISender sender) =>
        {
            var result = await sender.Send(new UpdateVoucherCommand(id, request));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithSummary("Update voucher")
        .RequireAuthorization("Admin");

        // DELETE /api/vouchers/{id} - Admin only
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteVoucherCommand(id));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithSummary("Delete voucher")
        .RequireAuthorization("Admin");

        // POST /api/vouchers/validate - Public (for customers to check vouchers)
        group.MapPost("/validate", async (ValidateVoucherRequest request, ISender sender) =>
        {
            var result = await sender.Send(new ValidateVoucherQuery(
                request.Code,
                request.CartTotal,
                request.ProductIds,
                request.CategoryId,
                request.CustomerId,
                request.CustomerTier));

            return Results.Ok(result);
        })
        .WithSummary("Validate voucher for cart")
        .WithDescription("Returns discount amount if valid, or error message if not");
        // Public endpoint - no auth required for validation
    }
}

// Request model for validation endpoint
public record ValidateVoucherRequest(
    string Code,
    decimal CartTotal,
    List<Guid>? ProductIds = null,
    Guid? CategoryId = null,
    Guid? CustomerId = null,
    int? CustomerTier = null);

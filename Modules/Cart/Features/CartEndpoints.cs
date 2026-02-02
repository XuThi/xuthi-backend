namespace Cart.Features;

// TODO: Seperate all of this to different files

public class CartEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cart")
            .WithTags("Shopping Cart");

        // GET /api/cart - Get cart by session or customer ID
        group.MapGet("/", async (
            string? sessionId,
            Guid? customerId,
            ISender sender) =>
        {
            if (string.IsNullOrEmpty(sessionId) && !customerId.HasValue)
                return Results.BadRequest("Either sessionId or customerId must be provided");

            var result = await sender.Send(new GetCartQuery(sessionId, customerId));
            return result.Cart is null 
                ? Results.Ok(new { items = Array.Empty<object>(), total = 0, subtotal = 0 }) 
                : Results.Ok(result.Cart);
        })
        .WithSummary("Get shopping cart")
        .WithDescription("Get cart by sessionId (anonymous) or customerId (logged in)");

        // POST /api/cart/items - Add item to cart
        group.MapPost("/items", async (AddToCartRequest request, ISender sender) =>
        {
            var result = await sender.Send(new AddToCartCommand(
                request.SessionId,
                request.CustomerId,
                request.ProductId,
                request.VariantId,
                request.Quantity));
            return Results.Ok(result.Cart);
        })
        .WithSummary("Add item to cart")
        .WithDescription("Add product variant to cart. Creates cart if doesn't exist.");

        // PUT /api/cart/{cartId}/items/{variantId} - Update item quantity
        group.MapPut("/{cartId:guid}/items/{variantId:guid}", async (
            Guid cartId,
            Guid variantId,
            UpdateCartItemRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateCartItemCommand(cartId, variantId, request.Quantity));
            return result.Success 
                ? Results.Ok(result.Cart) 
                : Results.BadRequest(result.ErrorMessage);
        })
        .WithSummary("Update item quantity")
        .WithDescription("Update quantity of item in cart. Set to 0 to remove.");

        // DELETE /api/cart/{cartId}/items/{variantId} - Remove item from cart
        group.MapDelete("/{cartId:guid}/items/{variantId:guid}", async (
            Guid cartId,
            Guid variantId,
            ISender sender) =>
        {
            var result = await sender.Send(new RemoveFromCartCommand(cartId, variantId));
            return result.Success ? Results.Ok(result.Cart) : Results.NotFound();
        })
        .WithSummary("Remove item from cart");

        // DELETE /api/cart/{cartId} - Clear cart
        group.MapDelete("/{cartId:guid}", async (Guid cartId, ISender sender) =>
        {
            var result = await sender.Send(new ClearCartCommand(cartId));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithSummary("Clear entire cart");

        // POST /api/cart/{cartId}/voucher - Apply voucher
        group.MapPost("/{cartId:guid}/voucher", async (
            Guid cartId,
            ApplyVoucherRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new ApplyVoucherCommand(cartId, request.VoucherCode));
            return result.Success 
                ? Results.Ok(new { discountAmount = result.DiscountAmount, cart = result.Cart })
                : Results.BadRequest(new { error = result.ErrorMessage });
        })
        .WithSummary("Apply voucher to cart")
        .WithDescription("Validates and applies voucher discount");

        // DELETE /api/cart/{cartId}/voucher - Remove voucher
        group.MapDelete("/{cartId:guid}/voucher", async (Guid cartId, ISender sender) =>
        {
            var result = await sender.Send(new RemoveVoucherCommand(cartId));
            return result.Success ? Results.Ok(result.Cart) : Results.NotFound();
        })
        .WithSummary("Remove voucher from cart");

        // POST /api/cart/{cartId}/sync - Sync prices with catalog
        group.MapPost("/{cartId:guid}/sync", async (Guid cartId, ISender sender) =>
        {
            var result = await sender.Send(new SyncCartPricesCommand(cartId));
            return result.Success 
                ? Results.Ok(new { cart = result.Cart, warnings = result.Warnings })
                : Results.NotFound();
        })
        .WithSummary("Sync cart with catalog")
        .WithDescription("Updates prices and stock availability from ProductCatalog. Call before checkout.");

        // POST /api/cart/merge - Merge anonymous cart to customer cart
        group.MapPost("/merge", async (MergeCartsRequest request, ISender sender) =>
        {
            var result = await sender.Send(new MergeCartsCommand(request.SessionId, request.CustomerId));
            return result.Success ? Results.Ok(result.Cart) : Results.NotFound();
        })
        .WithSummary("Merge anonymous cart to customer")
        .WithDescription("After login, merge anonymous session cart into customer cart");
    }
}

// Request DTOs
public record AddToCartRequest(
    string? SessionId,
    Guid? CustomerId,
    Guid ProductId,
    Guid VariantId,
    int Quantity = 1);

public record UpdateCartItemRequest(int Quantity);

public record ApplyVoucherRequest(string VoucherCode);

public record MergeCartsRequest(string SessionId, Guid CustomerId);

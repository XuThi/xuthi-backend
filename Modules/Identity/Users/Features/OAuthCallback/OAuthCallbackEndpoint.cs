namespace Identity.Users.Features.OAuthCallback;

public record OAuthCallbackRequest(string? ReturnUrl);

public class OAuthCallbackEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/callback", async (
            [AsParameters] OAuthCallbackRequest request,
            OAuthCallbackHandler handler,
            HttpContext context,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration config) =>
        {
            return await handler.Handle(new OAuthCallbackQuery(request), context, signInManager, userManager, config);
        })
        .WithName("OAuthCallback")
        .Produces(StatusCodes.Status302Found)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("OAuth callback");
    }
}

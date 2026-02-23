namespace Identity.Users.Features.LoginGoogle;

public record LoginGoogleRequest(string? ReturnUrl);

public class LoginGoogleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/login-google", (
            [AsParameters] LoginGoogleRequest request,
            LoginGoogleHandler handler,
            HttpContext context,
            SignInManager<ApplicationUser> signInManager) =>
        {
            return handler.Handle(new LoginGoogleQuery(request), context, signInManager);
        })
        .WithName("LoginGoogle")
        .Produces(StatusCodes.Status302Found)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("Login with Google");
    }
}

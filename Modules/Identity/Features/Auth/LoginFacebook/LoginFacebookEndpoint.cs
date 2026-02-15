namespace Identity.Features.Auth.LoginFacebook;

public record LoginFacebookRequest(string? ReturnUrl);

public class LoginFacebookEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/login-facebook", (
            [AsParameters] LoginFacebookRequest request,
            LoginFacebookHandler handler,
            HttpContext context,
            SignInManager<ApplicationUser> signInManager) =>
        {
            return handler.Handle(new LoginFacebookQuery(request), context, signInManager);
        })
        .WithName("LoginFacebook")
        .Produces(StatusCodes.Status302Found)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("Login with Facebook");
    }
}

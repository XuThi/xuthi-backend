using Identity.Users.Dtos;

namespace Identity.Users.Features.Login;

public record LoginRequest(string Email, string Password);

public class LoginEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            LoginHandler handler,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration config) =>
        {
            return await handler.Handle(new LoginCommand(request), userManager, signInManager, config);
        })
        .WithName("Login")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("Login")
        .WithDescription("Authenticate with email and password");
    }
}

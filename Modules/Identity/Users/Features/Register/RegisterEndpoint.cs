using Identity.Users.Dtos;

namespace Identity.Users.Features.Register;

public record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);

public class RegisterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (
            RegisterRequest request,
            RegisterHandler handler,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IConfiguration config,
            HttpContext httpContext) =>
        {
            return await handler.Handle(new RegisterCommand(request), userManager, emailService, config, httpContext);
        })
        .WithName("Register")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("Register")
        .WithDescription("Register a new user and send verification email");
    }
}

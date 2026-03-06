using Identity.Users.Dtos;

namespace Identity.Users.Features.ForgotPassword;

public record ForgotPasswordRequest(string Email);

public class ForgotPasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/forgot-password", async (
            ForgotPasswordRequest request,
            ForgotPasswordHandler handler,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IConfiguration config,
            HttpContext httpContext) =>
        {
            return await handler.Handle(request, userManager, emailService, config, httpContext);
        })
        .WithName("ForgotPassword")
        .Produces<MessageResponse>(StatusCodes.Status200OK)
        .WithTags("Authentication")
        .WithSummary("Forgot Password")
        .WithDescription("Send a password reset link to the user's email");
    }
}

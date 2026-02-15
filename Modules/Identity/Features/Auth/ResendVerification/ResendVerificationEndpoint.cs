using Identity.Features.Auth.Shared;

namespace Identity.Features.Auth.ResendVerification;

public record ResendVerificationRequest(string Email);

public class ResendVerificationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/resend-verification", async (
            ResendVerificationRequest request,
            ResendVerificationHandler handler,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IConfiguration config,
            HttpContext httpContext) =>
        {
            return await handler.Handle(new ResendVerificationCommand(request), userManager, emailService, config, httpContext);
        })
        .WithName("ResendVerification")
        .Produces<MessageResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithTags("Authentication")
        .WithSummary("Resend verification email");
    }
}

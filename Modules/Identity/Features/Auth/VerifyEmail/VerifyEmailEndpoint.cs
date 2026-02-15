using Identity.Features.Auth.Shared;

namespace Identity.Features.Auth.VerifyEmail;

public record VerifyEmailRequest(string UserId, string Token);

public class VerifyEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/verify-email", async (
            [AsParameters] VerifyEmailRequest request,
            VerifyEmailHandler handler,
            UserManager<ApplicationUser> userManager,
            IConfiguration config) =>
        {
            return await handler.Handle(new VerifyEmailQuery(request), userManager, config);
        })
        .WithName("VerifyEmail")
        .Produces<VerifyEmailResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("Verify email")
        .WithDescription("Verify email with token");
    }
}

using Identity.Features.Auth.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Features.Auth.VerifyEmail;

// TODO: Fuck this HTTPRequest

public record VerifyEmailRequest(string UserId, string Token);

public class VerifyEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/verify-email", async (
            HttpRequest httpRequest,
            [FromServices] VerifyEmailHandler handler,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] IConfiguration config) =>
        {
            var userId = httpRequest.Query["userId"].FirstOrDefault()
                ?? httpRequest.Query["UserId"].FirstOrDefault()
                ?? string.Empty;

            var token = httpRequest.Query["token"].FirstOrDefault()
                ?? httpRequest.Query["Token"].FirstOrDefault()
                ?? string.Empty;

            var request = new VerifyEmailRequest(userId, token);
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

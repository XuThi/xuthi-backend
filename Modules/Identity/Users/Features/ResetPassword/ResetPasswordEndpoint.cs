using Identity.Users.Dtos;

namespace Identity.Users.Features.ResetPassword;

public record ResetPasswordRequest(string UserId, string Token, string NewPassword);

public class ResetPasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/reset-password", async (
            ResetPasswordRequest request,
            ResetPasswordHandler handler,
            UserManager<ApplicationUser> userManager) =>
        {
            return await handler.Handle(request, userManager);
        })
        .WithName("ResetPassword")
        .Produces<MessageResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("Reset Password")
        .WithDescription("Reset password using the token from the email link");
    }
}

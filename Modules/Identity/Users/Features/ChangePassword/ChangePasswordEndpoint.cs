using Identity.Users.Dtos;

namespace Identity.Users.Features.ChangePassword;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public class ChangePasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/change-password", async (
            ChangePasswordRequest request,
            ChangePasswordHandler handler,
            UserManager<ApplicationUser> userManager,
            HttpContext httpContext) =>
        {
            return await handler.Handle(request, userManager, httpContext);
        })
        .RequireAuthorization()
        .WithName("ChangePassword")
        .Produces<MessageResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("Change Password")
        .WithDescription("Change password for the currently authenticated user");
    }
}

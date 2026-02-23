using Identity.Users.Dtos;

namespace Identity.Users.Features.GetCurrentUser;

public class GetCurrentUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", async (
            ClaimsPrincipal principal,
            GetCurrentUserHandler handler,
            UserManager<ApplicationUser> userManager) =>
        {
            return await handler.Handle(new GetCurrentUserQuery(principal), userManager);
        })
        .WithName("GetCurrentUser")
        .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .WithTags("Authentication")
        .WithSummary("Get current user")
        .RequireAuthorization();
    }
}

using Identity.Features.Auth.Shared;

namespace Identity.Features.Auth.Logout;

public class LogoutEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/logout", (LogoutHandler handler) =>
        {
            return handler.Handle(new LogoutCommand());
        })
        .WithName("Logout")
        .Produces<MessageResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Authentication")
        .WithSummary("Logout")
        .WithDescription("Client-side logout for JWT");
    }
}

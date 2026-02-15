using Identity.Features.Auth.Shared;

namespace Identity.Features.Auth.Logout;

public record LogoutCommand();

public class LogoutHandler
{
    public IResult Handle(LogoutCommand command)
    {
        return Results.Ok(new MessageResponse("Logged out successfully"));
    }
}

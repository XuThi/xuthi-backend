using Identity.Users.Dtos;

namespace Identity.Users.Features.Logout;

public record LogoutCommand();

public class LogoutHandler
{
    public IResult Handle(LogoutCommand command)
    {
        return Results.Ok(new MessageResponse("Logged out successfully"));
    }
}

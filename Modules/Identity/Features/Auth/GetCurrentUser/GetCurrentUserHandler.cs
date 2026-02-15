using Identity.Features.Auth.Shared;
using Identity.Infrastructure.Entity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Identity.Features.Auth.GetCurrentUser;

public record GetCurrentUserQuery(ClaimsPrincipal Principal);

public class GetCurrentUserHandler
{
    public async Task<IResult> Handle(GetCurrentUserQuery query, UserManager<ApplicationUser> userManager)
    {
        var userId = query.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new CurrentUserResponse(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.AvatarUrl,
            user.EmailConfirmed,
            roles.ToArray()
        ));
    }
}

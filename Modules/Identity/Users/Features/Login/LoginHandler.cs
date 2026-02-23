using Identity.Users.Dtos;
using Identity.Users.Features.Shared;

namespace Identity.Users.Features.Login;

public record LoginCommand(LoginRequest Request);

public class LoginHandler
{
    public async Task<IResult> Handle(
        LoginCommand command,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration config)
    {
        var request = command.Request;

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            return Results.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);

        if (!result.Succeeded)
        {
            return Results.Unauthorized();
        }

        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var token = AuthHelpers.GenerateJwtToken(user, roles, config);

        return Results.Ok(new AuthResponse(
            token,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.Id,
            user.EmailConfirmed,
            roles.ToArray()
        ));
    }
}

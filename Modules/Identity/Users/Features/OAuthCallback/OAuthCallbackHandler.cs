using Identity.Users.Dtos;
using Identity.Users.Features.Shared;

namespace Identity.Users.Features.OAuthCallback;

public record OAuthCallbackQuery(OAuthCallbackRequest Request);

public class OAuthCallbackHandler
{
    public async Task<IResult> Handle(
        OAuthCallbackQuery query,
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        var returnUrl = query.Request.ReturnUrl ?? "/";
        var info = await signInManager.GetExternalLoginInfoAsync();

        if (info == null)
        {
            return Results.Redirect($"{returnUrl}?error=oauth-failed");
        }

        var signInResult = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false);

        ApplicationUser? user;

        if (signInResult.Succeeded)
        {
            user = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        }
        else
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
            var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);

            if (string.IsNullOrEmpty(email))
            {
                return Results.Redirect($"{returnUrl}?error=no-email");
            }

            user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    return Results.Redirect($"{returnUrl}?error=create-failed");
                }

                await userManager.AddToRoleAsync(user, "Customer");
            }

            await userManager.AddLoginAsync(user, info);
        }

        if (user == null)
        {
            return Results.Redirect($"{returnUrl}?error=user-not-found");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var token = AuthHelpers.GenerateJwtToken(user, roles, config);

        var separator = returnUrl.Contains('?') ? "&" : "?";
        return Results.Redirect($"{returnUrl}{separator}token={token}");
    }
}

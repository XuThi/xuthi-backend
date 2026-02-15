using Identity.Features.Auth.Shared;
using Identity.Infrastructure.Entity;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features.Auth.VerifyEmail;

public record VerifyEmailQuery(VerifyEmailRequest Request);

public class VerifyEmailHandler
{
    public async Task<IResult> Handle(
        VerifyEmailQuery query,
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        var request = query.Request;

        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Token))
        {
            return Results.BadRequest(new ErrorResponse("Invalid verification link"));
        }

        var user = await userManager.FindByIdAsync(request.UserId);

        if (user == null)
        {
            return Results.BadRequest(new ErrorResponse("User not found"));
        }

        if (user.EmailConfirmed)
        {
            return Results.Ok(new VerifyEmailResponse("Email already verified", true));
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new ErrorResponse("Invalid or expired token", result.Errors.Select(e => e.Description)));
        }

        var roles = await userManager.GetRolesAsync(user);
        var jwtToken = AuthHelpers.GenerateJwtToken(user, roles, config);

        return Results.Ok(new VerifyEmailResponse(
            "Email verified successfully",
            false,
            jwtToken,
            user.Email
        ));
    }
}

using Identity.Features.Auth.Shared;
using Identity.Infrastructure.Entity;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features.Auth.ResendVerification;

public record ResendVerificationCommand(ResendVerificationRequest Request);

public class ResendVerificationHandler
{
    public async Task<IResult> Handle(
        ResendVerificationCommand command,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration config,
        HttpContext httpContext)
    {
        var request = command.Request;

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            return Results.Ok(new MessageResponse("If that email exists, a verification email has been sent"));
        }

        if (user.EmailConfirmed)
        {
            return Results.Ok(new MessageResponse("Email is already verified"));
        }

        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

        var frontendUrl = config["FrontendUrl"] ?? AuthHelpers.GetFrontendUrl(httpContext);
        var verificationLink = $"{frontendUrl}/auth/verify-email?userId={user.Id}&token={Uri.EscapeDataString(emailToken)}";

        try
        {
            await emailService.SendVerificationEmailAsync(user.Email!, verificationLink);
        }
        catch
        {
            return Results.Problem("Failed to send verification email");
        }

        return Results.Ok(new MessageResponse("Verification email sent"));
    }
}

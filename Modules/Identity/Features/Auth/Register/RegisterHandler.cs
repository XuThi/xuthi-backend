using Identity.Features.Auth.Shared;
using Identity.Infrastructure.Entity;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features.Auth.Register;

public record RegisterCommand(RegisterRequest Request);

public class RegisterHandler
{
    public async Task<IResult> Handle(
        RegisterCommand command,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration config,
        HttpContext httpContext)
    {
        var request = command.Request;

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new ErrorResponse("Registration failed", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, "Customer");

        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

        var frontendUrl = config["FrontendUrl"] ?? AuthHelpers.GetFrontendUrl(httpContext);
        var verificationLink = $"{frontendUrl}/auth/verify-email?userId={user.Id}&token={Uri.EscapeDataString(emailToken)}";

        try
        {
            await emailService.SendVerificationEmailAsync(user.Email!, verificationLink);
        }
        catch
        {
            // Ignore email failures
        }

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

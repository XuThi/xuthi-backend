using Identity.Users.Dtos;
using Identity.Users.Features.Shared;
using Microsoft.Extensions.Caching.Memory;

namespace Identity.Users.Features.ResendVerification;

public record ResendVerificationCommand(ResendVerificationRequest Request);

public class ResendVerificationHandler(IMemoryCache cache)
{
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(2);

    public async Task<IResult> Handle(
        ResendVerificationCommand command,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration config,
        HttpContext httpContext)
    {
        var request = command.Request;
        var rateLimitKey = $"verify-ratelimit:{request.Email.ToLowerInvariant()}";

        // Check rate limit - max 1 verification email per 2 minutes per email
        if (cache.TryGetValue(rateLimitKey, out _))
        {
            return Results.Ok(new MessageResponse("Please wait 2 minutes before sending another verification email."));
        }

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

        await emailService.SendVerificationEmailAsync(user.Email!, verificationLink);

        // Set rate limit after successful send
        cache.Set(rateLimitKey, true, RateLimitWindow);

        return Results.Ok(new MessageResponse("Verification email sent"));
    }
}

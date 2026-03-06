using Identity.Users.Dtos;
using Identity.Users.Features.Shared;

namespace Identity.Users.Features.ForgotPassword;

public class ForgotPasswordHandler
{
    public async Task<IResult> Handle(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration config,
        HttpContext httpContext)
    {
        // Always return success to avoid leaking user existence
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Ok(new MessageResponse("Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu."));
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        var frontendUrl = config["FrontendUrl"] ?? AuthHelpers.GetFrontendUrl(httpContext);
        var resetLink = $"{frontendUrl}/auth/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        await emailService.SendPasswordResetEmailAsync(user.Email!, resetLink);

        return Results.Ok(new MessageResponse("Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu."));
    }
}

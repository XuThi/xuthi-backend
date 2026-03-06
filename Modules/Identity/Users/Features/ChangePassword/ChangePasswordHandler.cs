using Identity.Users.Dtos;
using Microsoft.Extensions.Logging;

namespace Identity.Users.Features.ChangePassword;

public class ChangePasswordHandler
{
    private readonly ILogger<ChangePasswordHandler> _logger;

    public ChangePasswordHandler(ILogger<ChangePasswordHandler> logger)
    {
        _logger = logger;
    }

    public async Task<IResult> Handle(
        ChangePasswordRequest request,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("ChangePassword: No user ID in claims");
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            _logger.LogWarning("ChangePassword: User {UserId} not found", userId);
            return Results.Unauthorized();
        }

        // Check if user has a password (OAuth users may not)
        var hasPassword = await userManager.HasPasswordAsync(user);
        if (!hasPassword)
        {
            _logger.LogInformation("ChangePassword: User {UserId} has no password (OAuth account)", userId);
            return Results.BadRequest(new ErrorResponse(
                "Tài khoản này đăng nhập bằng mạng xã hội và chưa có mật khẩu. Vui lòng vào 'Quên mật khẩu' ở trang đăng nhập để tạo mật khẩu."));
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errorCodes = result.Errors.Select(e => e.Code).ToList();
            _logger.LogWarning("ChangePassword failed for user {UserId}: {Errors}",
                userId, string.Join(", ", errorCodes));

            if (errorCodes.Contains("PasswordMismatch"))
            {
                return Results.BadRequest(new ErrorResponse("Mật khẩu hiện tại không đúng."));
            }

            // Translate common identity errors
            var messages = result.Errors.Select(e => e.Code switch
            {
                "PasswordTooShort" => "Mật khẩu phải có ít nhất 6 ký tự.",
                "PasswordRequiresDigit" => "Mật khẩu phải chứa ít nhất một chữ số.",
                "PasswordRequiresLower" => "Mật khẩu phải chứa ít nhất một chữ thường.",
                "PasswordRequiresUpper" => "Mật khẩu phải chứa ít nhất một chữ hoa.",
                _ => e.Description
            });
            return Results.BadRequest(new ErrorResponse("Đổi mật khẩu thất bại: " + string.Join(" ", messages)));
        }

        _logger.LogInformation("ChangePassword: Success for user {UserId}", userId);
        return Results.Ok(new MessageResponse("Mật khẩu đã được đổi thành công."));
    }
}

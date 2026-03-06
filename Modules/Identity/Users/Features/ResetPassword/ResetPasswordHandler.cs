using Identity.Users.Dtos;
using Microsoft.Extensions.Logging;

namespace Identity.Users.Features.ResetPassword;

public class ResetPasswordHandler
{
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(ILogger<ResetPasswordHandler> logger)
    {
        _logger = logger;
    }

    public async Task<IResult> Handle(
        ResetPasswordRequest request,
        UserManager<ApplicationUser> userManager)
    {
        if (!Guid.TryParse(request.UserId, out _))
        {
            return Results.BadRequest(new ErrorResponse("Link đặt lại mật khẩu không hợp lệ."));
        }

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            _logger.LogWarning("ResetPassword: User {UserId} not found", request.UserId);
            return Results.BadRequest(new ErrorResponse("Link đặt lại mật khẩu không hợp lệ hoặc đã hết hạn."));
        }

        _logger.LogInformation("ResetPassword: Attempting reset for user {UserId}, token length: {TokenLength}",
            request.UserId, request.Token?.Length ?? 0);

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errorCodes = result.Errors.Select(e => e.Code).ToList();
            _logger.LogWarning("ResetPassword failed for user {UserId}: {Errors}",
                request.UserId, string.Join(", ", errorCodes));

            if (errorCodes.Contains("InvalidToken"))
            {
                return Results.BadRequest(new ErrorResponse(
                    "Link đặt lại mật khẩu đã hết hạn hoặc đã được sử dụng. Vui lòng yêu cầu link mới."));
            }

            var messages = result.Errors.Select(e => e.Code switch
            {
                "PasswordTooShort" => "Mật khẩu phải có ít nhất 6 ký tự.",
                "PasswordRequiresDigit" => "Mật khẩu phải chứa ít nhất một chữ số.",
                "PasswordRequiresLower" => "Mật khẩu phải chứa ít nhất một chữ thường.",
                _ => e.Description
            });
            return Results.BadRequest(new ErrorResponse("Đặt lại mật khẩu thất bại: " + string.Join(" ", messages)));
        }

        _logger.LogInformation("ResetPassword: Success for user {UserId}", request.UserId);
        return Results.Ok(new MessageResponse("Mật khẩu đã được đặt lại thành công. Bạn có thể đăng nhập với mật khẩu mới."));
    }
}

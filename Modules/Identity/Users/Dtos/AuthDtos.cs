namespace Identity.Users.Dtos;

public record AuthResponse(
    string Token,
    string Email,
    string? FirstName,
    string? LastName,
    Guid UserId,
    bool EmailConfirmed,
    string[] Roles
);

public record CurrentUserResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    bool EmailConfirmed,
    string[] Roles
);

public record MessageResponse(string Message);

public record VerifyEmailResponse(
    string Message,
    bool? AlreadyVerified = null,
    string? Token = null,
    string? Email = null
);

public record ErrorResponse(string Error, IEnumerable<string>? Errors = null);

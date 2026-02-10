using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Carter;
using Identity.Infrastructure.Entity;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Features.Auth;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
public record AuthResponse(string Token, string Email, string? FirstName, string? LastName, Guid UserId, bool EmailConfirmed, string[] Roles);
public record RefreshTokenRequest(string Token);
public record ResendVerificationRequest(string Email);
public record VerifyEmailRequest(string UserId, string Token);

public class AuthEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");
        
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapGet("/me", GetCurrentUser).RequireAuthorization();
        group.MapPost("/logout", Logout);
        
        // Email verification
        group.MapGet("/verify-email", VerifyEmail);
        group.MapPost("/resend-verification", ResendVerification);
        
        // OAuth endpoints
        group.MapGet("/login-google", LoginGoogle);
        group.MapGet("/login-facebook", LoginFacebook);
        group.MapGet("/callback", OAuthCallback);
    }
    
    private static async Task<IResult> Register(
        [FromBody] RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration config,
        HttpContext httpContext)
    {
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
            return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }
        
        // Assign Customer role by default
        await userManager.AddToRoleAsync(user, "Customer");
        
        // Generate email confirmation token
        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        
        // Build verification URL
        var frontendUrl = config["FrontendUrl"] ?? GetFrontendUrl(httpContext);
        var verificationLink = $"{frontendUrl}/auth/verify-email?userId={user.Id}&token={Uri.EscapeDataString(emailToken)}";
        
        // Send verification email
        try
        {
            await emailService.SendVerificationEmailAsync(user.Email!, verificationLink);
        }
        catch
        {
            // Log but don't fail registration if email fails
        }
        
        var roles = await userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles, config);
        
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
    
    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration config)
    {
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
        
        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        
        var roles = await userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles, config);
        
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
    
    private static async Task<IResult> VerifyEmail(
        [FromQuery] string userId,
        [FromQuery] string token,
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return Results.BadRequest(new { error = "Invalid verification link" });
        }
        
        var user = await userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            return Results.BadRequest(new { error = "User not found" });
        }
        
        if (user.EmailConfirmed)
        {
            return Results.Ok(new { message = "Email already verified", alreadyVerified = true });
        }
        
        var result = await userManager.ConfirmEmailAsync(user, token);
        
        if (!result.Succeeded)
        {
            return Results.BadRequest(new { error = "Invalid or expired token", errors = result.Errors.Select(e => e.Description) });
        }
        
        // Generate new JWT with updated email confirmed status
        var roles = await userManager.GetRolesAsync(user);
        var jwtToken = GenerateJwtToken(user, roles, config);
        
        return Results.Ok(new { 
            message = "Email verified successfully", 
            token = jwtToken,
            email = user.Email
        });
    }
    
    private static async Task<IResult> ResendVerification(
        [FromBody] ResendVerificationRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration config,
        HttpContext httpContext)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        
        if (user == null)
        {
            // Don't reveal that user doesn't exist
            return Results.Ok(new { message = "If that email exists, a verification email has been sent" });
        }
        
        if (user.EmailConfirmed)
        {
            return Results.Ok(new { message = "Email is already verified" });
        }
        
        // Generate new email confirmation token
        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        
        // Build verification URL
        var frontendUrl = config["FrontendUrl"] ?? GetFrontendUrl(httpContext);
        var verificationLink = $"{frontendUrl}/auth/verify-email?userId={user.Id}&token={Uri.EscapeDataString(emailToken)}";
        
        try
        {
            await emailService.SendVerificationEmailAsync(user.Email!, verificationLink);
        }
        catch
        {
            return Results.Problem("Failed to send verification email");
        }
        
        return Results.Ok(new { message = "Verification email sent" });
    }
    
    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }
        
        var user = await userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            return Results.Unauthorized();
        }
        
        var roles = await userManager.GetRolesAsync(user);
        
        return Results.Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.AvatarUrl,
            user.EmailConfirmed,
            Roles = roles.ToArray()
        });
    }
    
    private static IResult Logout()
    {
        // For JWT, logout is handled client-side by removing the token
        return Results.Ok(new { message = "Logged out successfully" });
    }
    
    private static IResult LoginGoogle(
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        [FromQuery] string? returnUrl)
    {
        var redirectUrl = $"/api/auth/callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
        var properties = signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
        return Results.Challenge(properties, ["Google"]);
    }
    
    private static IResult LoginFacebook(
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        [FromQuery] string? returnUrl)
    {
        var redirectUrl = $"/api/auth/callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
        var properties = signInManager.ConfigureExternalAuthenticationProperties("Facebook", redirectUrl);
        return Results.Challenge(properties, ["Facebook"]);
    }
    
    private static async Task<IResult> OAuthCallback(
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        [FromQuery] string? returnUrl)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        
        if (info == null)
        {
            return Results.Redirect($"{returnUrl ?? "/"}?error=oauth-failed");
        }
        
        // Try to sign in with existing external login
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
            // Create new user from OAuth info
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
            var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);
            
            if (string.IsNullOrEmpty(email))
            {
                return Results.Redirect($"{returnUrl ?? "/"}?error=no-email");
            }
            
            // Check if user exists with this email
            user = await userManager.FindByEmailAsync(email);
            
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true // OAuth emails are verified
                };
                
                var createResult = await userManager.CreateAsync(user);
                
                if (!createResult.Succeeded)
                {
                    return Results.Redirect($"{returnUrl ?? "/"}?error=create-failed");
                }
                
                // Assign Customer role
                await userManager.AddToRoleAsync(user, "Customer");
            }
            
            // Link external login to user
            await userManager.AddLoginAsync(user, info);
        }
        
        if (user == null)
        {
            return Results.Redirect($"{returnUrl ?? "/"}?error=user-not-found");
        }
        
        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        
        // Generate JWT with roles
        var roles = await userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles, config);
        
        // Redirect back to frontend with token
        var separator = returnUrl?.Contains('?') == true ? "&" : "?";
        return Results.Redirect($"{returnUrl ?? "/"}{separator}token={token}");
    }
    
    private static string GenerateJwtToken(ApplicationUser user, IList<string> roles, IConfiguration config)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? "YourSuperSecretKeyThatIsAtLeast32Characters!"));
        
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new Claim("email_confirmed", user.EmailConfirmed.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        // Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? "XuThiApi",
            audience: config["Jwt:Audience"] ?? "XuThiFrontend",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    private static string GetFrontendUrl(HttpContext context)
    {
        // Try to get the frontend URL from the Referer header
        var referer = context.Request.Headers["Referer"].FirstOrDefault();
        if (!string.IsNullOrEmpty(referer))
        {
            var uri = new Uri(referer);
            return $"{uri.Scheme}://{uri.Authority}";
        }
        
        // Default fallback
        return "http://localhost:3000";
    }
}

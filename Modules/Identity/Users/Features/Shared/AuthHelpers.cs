using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Users.Models;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Users.Features.Shared;

public static class AuthHelpers
{
    public static string GenerateJwtToken(ApplicationUser user, IList<string> roles, IConfiguration config)
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

    public static string GetFrontendUrl(HttpContext context)
    {
        var referer = context.Request.Headers["Referer"].FirstOrDefault();
        if (!string.IsNullOrEmpty(referer))
        {
            var uri = new Uri(referer);
            return $"{uri.Scheme}://{uri.Authority}";
        }

        return "http://localhost:3000";
    }
}

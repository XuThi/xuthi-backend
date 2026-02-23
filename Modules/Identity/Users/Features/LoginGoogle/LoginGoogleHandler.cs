namespace Identity.Users.Features.LoginGoogle;

public record LoginGoogleQuery(LoginGoogleRequest Request);

public class LoginGoogleHandler
{
    public IResult Handle(
        LoginGoogleQuery query,
        HttpContext context,
        SignInManager<ApplicationUser> signInManager)
    {
        var redirectUrl = $"/api/auth/callback?returnUrl={Uri.EscapeDataString(query.Request.ReturnUrl ?? "/")}";
        var properties = signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
        return Results.Challenge(properties, ["Google"]);
    }
}

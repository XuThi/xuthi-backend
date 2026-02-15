namespace Identity.Features.Auth.LoginFacebook;

public record LoginFacebookQuery(LoginFacebookRequest Request);

public class LoginFacebookHandler
{
    public IResult Handle(
        LoginFacebookQuery query,
        HttpContext context,
        SignInManager<ApplicationUser> signInManager)
    {
        var redirectUrl = $"/api/auth/callback?returnUrl={Uri.EscapeDataString(query.Request.ReturnUrl ?? "/")}";
        var properties = signInManager.ConfigureExternalAuthenticationProperties("Facebook", redirectUrl);
        return Results.Challenge(properties, ["Facebook"]);
    }
}

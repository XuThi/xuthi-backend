using Identity.Features.Auth.GetCurrentUser;
using Identity.Features.Auth.Login;
using Identity.Features.Auth.LoginFacebook;
using Identity.Features.Auth.LoginGoogle;
using Identity.Features.Auth.Logout;
using Identity.Features.Auth.OAuthCallback;
using Identity.Features.Auth.Register;
using Identity.Features.Auth.ResendVerification;
using Identity.Features.Auth.VerifyEmail;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Entity;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity;

public static class IdentityModule
{
    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        // Add IdentityDbContext with Aspire (same database as other modules)
        builder.AddNpgsqlDbContext<IdentityDbContext>("appdata");
        
        // Add Identity services
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            
            // User settings
            options.User.RequireUniqueEmail = true;
            
            // SignIn settings - allow login without verified email but track it
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();
        
        // Add Email service
        builder.Services.AddScoped<IEmailService, EmailService>();

        // Add auth feature handlers used by endpoint parameter binding
        builder.Services.AddScoped<GetCurrentUserHandler>();
        builder.Services.AddScoped<LoginHandler>();
        builder.Services.AddScoped<LoginGoogleHandler>();
        builder.Services.AddScoped<LoginFacebookHandler>();
        builder.Services.AddScoped<LogoutHandler>();
        builder.Services.AddScoped<OAuthCallbackHandler>();
        builder.Services.AddScoped<RegisterHandler>();
        builder.Services.AddScoped<ResendVerificationHandler>();
        builder.Services.AddScoped<VerifyEmailHandler>();
        
        return builder;
    }
}

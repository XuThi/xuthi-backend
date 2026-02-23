using Identity.Users.Features.GetCurrentUser;
using Identity.Users.Features.Login;
using Identity.Users.Features.LoginFacebook;
using Identity.Users.Features.LoginGoogle;
using Identity.Users.Features.Logout;
using Identity.Users.Features.OAuthCallback;
using Identity.Users.Features.Register;
using Identity.Users.Features.ResendVerification;
using Identity.Users.Features.VerifyEmail;
using Identity.Data;
using Identity.Users.Models;
using Identity.Users.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity;

public static class IdentityModule
{
    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("appdata"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichNpgsqlDbContext<IdentityDbContext>();
        
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

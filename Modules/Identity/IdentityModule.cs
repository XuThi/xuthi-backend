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
        
        return builder;
    }
}

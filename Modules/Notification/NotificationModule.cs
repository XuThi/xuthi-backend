using Identity.Users.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Notification;

public static class NotificationModule
{
    public static IHostApplicationBuilder AddNotificationModule(this IHostApplicationBuilder builder)
    {
        // IEmailService is already registered by IdentityModule.
        // This module only provides domain event handlers that consume it.
        return builder;
    }
}

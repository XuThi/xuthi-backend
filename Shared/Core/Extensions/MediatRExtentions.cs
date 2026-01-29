using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Core.Extensions;

public static class MediatRExtensions
{
    public static IServiceCollection AddMediatRWithAssemblies(
        this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(assemblies);
        });

        services.AddValidatorsFromAssemblies(assemblies);

        return services;
    }
}
using BillFoundry.Application.Configuration;
using BillFoundry.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(TimeProvider.System);

        services.AddOptions<DemoModeOptions>()
            .Bind(configuration.GetSection(DemoModeOptions.SectionName));

        services.AddOptions<IdentitySeedOptions>()
            .Bind(configuration.GetSection(IdentitySeedOptions.SectionName));

        services.AddSingleton<IDemoMode, DemoMode>();
        services.AddScoped<ICurrentUser, UnauthenticatedCurrentUser>();
        services.AddSingleton<IAuthorizationHandler, NotDemoModeHandler>();
        services.AddAuthorizationCore(AuthorizationPolicies.Configure);

        return services;
    }
}

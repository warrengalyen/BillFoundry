using BillFoundry.Application;
using BillFoundry.Application.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_registers_system_TimeProvider()
    {
        using ServiceProvider provider = CreateProvider();

        var timeProvider = provider.GetService<TimeProvider>();

        Assert.NotNull(timeProvider);
        Assert.Same(TimeProvider.System, timeProvider);
    }

    [Fact]
    public void AddApplication_registers_unauthenticated_current_user()
    {
        using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();

        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.IsType<UnauthenticatedCurrentUser>(currentUser);
        Assert.False(currentUser.IsAuthenticated);
    }

    [Fact]
    public void AddApplication_registers_demo_mode()
    {
        using ServiceProvider provider = CreateProvider();

        var demoMode = provider.GetRequiredService<IDemoMode>();

        Assert.False(demoMode.IsEnabled);
    }

    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddApplication(configuration);
        return services.BuildServiceProvider();
    }
}

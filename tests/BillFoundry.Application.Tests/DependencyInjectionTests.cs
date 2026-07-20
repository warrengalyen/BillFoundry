using BillFoundry.Application;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_registers_system_TimeProvider()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        var timeProvider = provider.GetService<TimeProvider>();

        Assert.NotNull(timeProvider);
        Assert.Same(TimeProvider.System, timeProvider);
    }
}

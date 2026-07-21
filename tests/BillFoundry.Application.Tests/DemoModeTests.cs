using BillFoundry.Application.Configuration;
using BillFoundry.Application.Security;
using Microsoft.Extensions.Options;

namespace BillFoundry.Application.Tests;

public sealed class DemoModeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Demo_mode_reflects_options(bool enabled)
    {
        var demoMode = new DemoMode(Options.Create(new DemoModeOptions { Enabled = enabled }));

        Assert.Equal(enabled, demoMode.IsEnabled);
    }
}

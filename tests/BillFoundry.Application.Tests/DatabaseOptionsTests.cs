using BillFoundry.Application.Configuration;

namespace BillFoundry.Application.Tests;

public sealed class DatabaseOptionsTests
{
    [Fact]
    public void ApplyMigrationsOnStartup_defaults_to_false()
    {
        var options = new DatabaseOptions();

        Assert.False(options.ApplyMigrationsOnStartup);
        Assert.Equal(30, options.CommandTimeoutSeconds);
        Assert.Equal("BillFoundry", DatabaseOptions.ConnectionStringName);
    }
}

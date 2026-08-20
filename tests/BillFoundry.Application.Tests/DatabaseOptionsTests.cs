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
        Assert.Equal(DatabaseProvider.SqlServer, options.Provider);
    }

    [Fact]
    public void Provider_can_be_set_to_postgresql_for_the_hosted_demo()
    {
        var options = new DatabaseOptions { Provider = DatabaseProvider.PostgreSql };

        Assert.Equal(DatabaseProvider.PostgreSql, options.Provider);
    }
}

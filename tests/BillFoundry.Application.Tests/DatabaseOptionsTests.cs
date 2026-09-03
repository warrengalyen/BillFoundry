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
        Assert.Equal(DatabaseProvider.PostgreSql, options.Provider);
    }

    [Fact]
    public void Provider_can_be_set_to_sql_server()
    {
        var options = new DatabaseOptions { Provider = DatabaseProvider.SqlServer };

        Assert.Equal(DatabaseProvider.SqlServer, options.Provider);
    }
}

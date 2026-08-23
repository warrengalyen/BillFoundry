using System.Net.Sockets;
using BillFoundry.Infrastructure.Persistence;
using Npgsql;

namespace BillFoundry.IntegrationTests;

public sealed class DatabaseMigrationStartupTests
{
    [Fact]
    public void Dns_failures_are_retried_during_startup_migrations()
    {
        var dns = new NpgsqlException(
            "Name or service not known",
            new SocketException(11001));

        Assert.True(DatabaseMigrationHostedService.IsTransientStartupFailure(dns));
    }

    [Fact]
    public void Sql_errors_are_not_retried_as_startup_connection_failures()
    {
        var syntax = new PostgresException(
            "syntax error",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.SyntaxError);

        Assert.False(DatabaseMigrationHostedService.IsTransientStartupFailure(syntax));
    }
}

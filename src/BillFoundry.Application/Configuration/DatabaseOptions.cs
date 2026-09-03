using System.ComponentModel.DataAnnotations;

namespace BillFoundry.Application.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "BillFoundry";

    /// <summary>
    /// Relational provider. Defaults to PostgreSQL. Set
    /// <c>Database__Provider=SqlServer</c> for the Microsoft database stack.
    /// </summary>
    public DatabaseProvider Provider { get; init; } = DatabaseProvider.PostgreSql;

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// When true, the host applies pending EF Core migrations at startup.
    /// This never drops or recreates the database. Default is false.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; init; }
}

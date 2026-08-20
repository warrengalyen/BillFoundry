namespace BillFoundry.Application.Configuration;

/// <summary>
/// Relational store used by EF Core. SQL Server is the Community default.
/// PostgreSQL exists so the public Render demo can use managed Postgres.
/// </summary>
public enum DatabaseProvider
{
    SqlServer = 0,
    PostgreSql = 1
}

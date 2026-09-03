namespace BillFoundry.Application.Configuration;

/// <summary>
/// Relational store used by EF Core. PostgreSQL is the Community default.
/// SQL Server remains a fully supported alternative.
/// </summary>
public enum DatabaseProvider
{
    SqlServer = 0,
    PostgreSql = 1
}

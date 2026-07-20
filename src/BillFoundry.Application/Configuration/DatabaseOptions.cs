using System.ComponentModel.DataAnnotations;

namespace BillFoundry.Application.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "BillFoundry";

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 30;
}

namespace BillFoundry.Application.Configuration;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public bool Enabled { get; init; }

    public string AdministratorEmail { get; init; } = "admin@localhost";

    public string? AdministratorPassword { get; init; }

    public string UserEmail { get; init; } = "user@localhost";

    public string? UserPassword { get; init; }
}

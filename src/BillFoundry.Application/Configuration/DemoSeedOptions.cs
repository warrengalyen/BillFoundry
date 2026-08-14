namespace BillFoundry.Application.Configuration;

/// <summary>
/// Optional fictional sample data for public demonstrations. Disabled by default.
/// Enabling this in a real business installation will create or reset demo accounts
/// and, when <see cref="ResetOnStartup"/> is true, replace business records.
/// </summary>
public sealed class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    public bool Enabled { get; init; }

    /// <summary>
    /// When true, existing clients, catalog items, estimates, invoices, payments,
    /// and audit events are removed and replaced with the published demo dataset.
    /// Organization profile and demo user passwords are restored. Default is false
    /// so a running public demo keeps visitor edits until an operator opts in.
    /// </summary>
    public bool ResetOnStartup { get; init; }

    public string AdministratorEmail { get; init; } = "admin@northbeacon.example";

    public string AdministratorPassword { get; init; } = "Demo-Admin-Passw0rd!";

    public string UserEmail { get; init; } = "user@northbeacon.example";

    public string UserPassword { get; init; } = "Demo-User-Passw0rd!";
}

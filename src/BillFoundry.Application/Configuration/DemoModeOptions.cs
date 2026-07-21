namespace BillFoundry.Application.Configuration;

public sealed class DemoModeOptions
{
    public const string SectionName = "DemoMode";

    public bool Enabled { get; init; }
}

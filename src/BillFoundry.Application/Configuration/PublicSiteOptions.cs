namespace BillFoundry.Application.Configuration;

/// <summary>
/// Public marketing links shown on the unauthenticated landing page.
/// </summary>
public sealed class PublicSiteOptions
{
    public const string SectionName = "PublicSite";

    public string RepositoryUrl { get; init; } = "https://github.com/warrengalyen/BillFoundry";
}

using System.ComponentModel.DataAnnotations;

namespace BillFoundry.Application.Configuration;

public sealed class OrganizationLogoStorageOptions
{
    public const string SectionName = "OrganizationLogoStorage";

    [Required]
    [MinLength(1)]
    public string RootPath { get; set; } = "App_Data/organization-logos";
}

using BillFoundry.Domain.Organizations;

namespace BillFoundry.Application.Organizations;

/// <summary>
/// Application-facing view of the installation's organization profile.
/// </summary>
public sealed class OrganizationSettingsDto
{
    public required string LegalName { get; init; }

    public required string DisplayName { get; init; }

    public required string AddressLine1 { get; init; }

    public string? AddressLine2 { get; init; }

    public required string City { get; init; }

    public string? Region { get; init; }

    public string? PostalCode { get; init; }

    public required string Country { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Website { get; init; }

    public string? TaxIdentifier { get; init; }

    public required string DefaultCurrency { get; init; }

    public required int DefaultPaymentTermsDays { get; init; }

    public required string DefaultInvoicePrefix { get; init; }

    public required string DefaultEstimatePrefix { get; init; }

    public string? DefaultInvoiceNotes { get; init; }

    public string? DefaultPaymentInstructions { get; init; }

    public string? LogoFileName { get; init; }

    public string? LogoContentType { get; init; }

    public long? LogoSizeBytes { get; init; }

    public bool HasLogo => LogoFileName is not null;

    public required byte[] RowVersion { get; init; }

    public static OrganizationSettingsDto From(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new OrganizationSettingsDto
        {
            LegalName = organization.LegalName,
            DisplayName = organization.DisplayName,
            AddressLine1 = organization.Address.Line1,
            AddressLine2 = organization.Address.Line2,
            City = organization.Address.City,
            Region = organization.Address.Region,
            PostalCode = organization.Address.PostalCode,
            Country = organization.Address.Country,
            Email = organization.Email,
            Phone = organization.Phone,
            Website = organization.Website,
            TaxIdentifier = organization.TaxIdentifier,
            DefaultCurrency = organization.DefaultCurrency.Value,
            DefaultPaymentTermsDays = organization.DefaultPaymentTermsDays,
            DefaultInvoicePrefix = organization.DefaultInvoicePrefix.Value,
            DefaultEstimatePrefix = organization.DefaultEstimatePrefix.Value,
            DefaultInvoiceNotes = organization.DefaultInvoiceNotes,
            DefaultPaymentInstructions = organization.DefaultPaymentInstructions,
            LogoFileName = organization.Logo?.StoredFileName,
            LogoContentType = organization.Logo?.ContentType,
            LogoSizeBytes = organization.Logo?.SizeBytes,
            RowVersion = organization.RowVersion
        };
    }
}

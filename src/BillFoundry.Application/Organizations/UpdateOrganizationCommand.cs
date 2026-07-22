namespace BillFoundry.Application.Organizations;

public sealed class UpdateOrganizationCommand
{
    public string LegalName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string? Region { get; set; }

    public string? PostalCode { get; set; }

    public string Country { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string? TaxIdentifier { get; set; }

    public string DefaultCurrency { get; set; } = "USD";

    public int DefaultPaymentTermsDays { get; set; } = 30;

    public string DefaultInvoicePrefix { get; set; } = "INV";

    public string DefaultEstimatePrefix { get; set; } = "EST";

    public string? DefaultInvoiceNotes { get; set; }

    public string? DefaultPaymentInstructions { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

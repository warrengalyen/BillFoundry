using BillFoundry.Application.Organizations;

namespace BillFoundry.Application.Tests;

public sealed class OrganizationSettingsValidatorTests
{
    [Fact]
    public void Validate_accepts_a_complete_profile()
    {
        IReadOnlyList<string> errors = OrganizationSettingsValidator.Validate(CreateValidCommand());

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_requires_identity_and_address_fields()
    {
        UpdateOrganizationCommand command = CreateValidCommand();
        command.LegalName = " ";
        command.DisplayName = "";
        command.AddressLine1 = null!;
        command.City = " ";
        command.Country = "";

        IReadOnlyList<string> errors = OrganizationSettingsValidator.Validate(command);

        Assert.Contains(errors, error => error.Contains("Legal name", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Display name", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Mailing address", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("City", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Country", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rejects_unsupported_currency_and_prefix()
    {
        UpdateOrganizationCommand command = CreateValidCommand();
        command.DefaultCurrency = "XXX";
        command.DefaultInvoicePrefix = "1INV";
        command.DefaultEstimatePrefix = "bad prefix";
        command.DefaultPaymentTermsDays = 400;
        command.Website = "javascript:alert(1)";
        command.Email = "not-an-email";
        command.RowVersion = [];

        IReadOnlyList<string> errors = OrganizationSettingsValidator.Validate(command);

        Assert.Contains(errors, error => error.Contains("currency", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Invoice prefix", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Estimate prefix", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("payment terms", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Website", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Email", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_rejects_control_characters()
    {
        UpdateOrganizationCommand command = CreateValidCommand();
        command.LegalName = "Acme\nLLC";

        IReadOnlyList<string> errors = OrganizationSettingsValidator.Validate(command);

        Assert.Contains(errors, error => error.Contains("control characters", StringComparison.Ordinal));
    }

    private static UpdateOrganizationCommand CreateValidCommand(byte[]? rowVersion = null) =>
        new()
        {
            LegalName = "Acme LLC",
            DisplayName = "Acme",
            AddressLine1 = "10 Main St",
            City = "Springfield",
            Region = "IL",
            PostalCode = "62701",
            Country = "United States",
            Email = "billing@acme.test",
            Phone = "555-0100",
            Website = "https://acme.test",
            TaxIdentifier = "12-3456789",
            DefaultCurrency = "USD",
            DefaultPaymentTermsDays = 30,
            DefaultInvoicePrefix = "INV",
            DefaultEstimatePrefix = "EST",
            DefaultInvoiceNotes = "Thank you.",
            DefaultPaymentInstructions = "Pay by transfer.",
            RowVersion = rowVersion ?? [1, 2, 3, 4]
        };
}

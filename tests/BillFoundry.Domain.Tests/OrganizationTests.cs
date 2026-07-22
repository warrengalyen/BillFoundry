using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Tests;

public sealed class OrganizationTests
{
    [Fact]
    public void CreateSingleton_uses_fixed_id_and_invoice_defaults()
    {
        Organization organization = Organization.CreateSingleton();

        Assert.Equal(Organization.SingletonId, organization.Id);
        Assert.Equal("USD", organization.DefaultCurrency.Value);
        Assert.Equal(30, organization.DefaultPaymentTermsDays);
        Assert.Equal("INV", organization.DefaultInvoicePrefix.Value);
        Assert.Equal("EST", organization.DefaultEstimatePrefix.Value);
        Assert.Equal(PostalAddress.Empty, organization.Address);
        Assert.Null(organization.Logo);
    }

    [Fact]
    public void UpdateProfile_replaces_business_settings()
    {
        Organization organization = Organization.CreateSingleton();

        organization.UpdateProfile(
            "Acme LLC",
            "Acme",
            PostalAddress.Create("10 Main St", null, "Springfield", "IL", "62701", "United States"),
            "billing@acme.test",
            "555-0100",
            "https://acme.test",
            "12-3456789",
            CurrencyCode.Parse("CAD"),
            14,
            DocumentPrefix.Parse("INV"),
            DocumentPrefix.Parse("EST"),
            "Thank you.",
            "Pay by transfer.");

        Assert.Equal("Acme LLC", organization.LegalName);
        Assert.Equal("Acme", organization.DisplayName);
        Assert.Equal("CAD", organization.DefaultCurrency.Value);
        Assert.Equal(14, organization.DefaultPaymentTermsDays);
        Assert.Equal("https://acme.test/", organization.Website);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void UpdateProfile_rejects_payment_terms_outside_range(int days)
    {
        Organization organization = Organization.CreateSingleton();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            organization.UpdateProfile(
                "Acme LLC",
                "Acme",
                PostalAddress.Create("10 Main St", null, "Springfield", "IL", "62701", "United States"),
                null,
                null,
                null,
                null,
                CurrencyCode.Usd,
                days,
                DocumentPrefix.InvoiceDefault,
                DocumentPrefix.EstimateDefault,
                null,
                null));
    }

    [Fact]
    public void SetLogo_rejects_path_segments_in_stored_name()
    {
        Assert.Throws<ArgumentException>(() =>
            new OrganizationLogo(@"..\evil.png", "image/png", 12));
        Assert.Throws<ArgumentException>(() =>
            new OrganizationLogo("folder/logo.png", "image/png", 12));
    }

    [Fact]
    public void ClearLogo_removes_logo_metadata()
    {
        Organization organization = Organization.CreateSingleton();
        organization.SetLogo(new OrganizationLogo($"{Guid.NewGuid():N}.png", "image/png", 24));

        organization.ClearLogo();

        Assert.Null(organization.Logo);
    }
}

using BillFoundry.Domain.Auditing;

namespace BillFoundry.Domain.Organizations;

/// <summary>
/// The single organization configured for a Community Edition installation.
/// </summary>
public sealed class Organization : IAuditable
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 50;
    public const int WebsiteMaxLength = 200;
    public const int TaxIdentifierMaxLength = 50;
    public const int NotesMaxLength = 4000;
    public const int MinPaymentTermsDays = 0;
    public const int MaxPaymentTermsDays = 365;

    /// <summary>
    /// Well-known identifier for the installation's only organization row.
    /// </summary>
    public static readonly Guid SingletonId = Guid.Parse("8f3e2c1a-9b7d-4e6f-a1c2-5d8e9f0a1b2c");

    private Organization()
    {
        LegalName = string.Empty;
        DisplayName = string.Empty;
        Address = PostalAddress.Empty;
        DefaultCurrency = CurrencyCode.Usd;
        DefaultInvoicePrefix = DocumentPrefix.InvoiceDefault;
        DefaultEstimatePrefix = DocumentPrefix.EstimateDefault;
        RowVersion = [];
    }

    public Guid Id { get; private set; }

    public string LegalName { get; private set; }

    public string DisplayName { get; private set; }

    public PostalAddress Address { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Website { get; private set; }

    public string? TaxIdentifier { get; private set; }

    public CurrencyCode DefaultCurrency { get; private set; }

    public int DefaultPaymentTermsDays { get; private set; }

    public DocumentPrefix DefaultInvoicePrefix { get; private set; }

    public DocumentPrefix DefaultEstimatePrefix { get; private set; }

    public string? DefaultInvoiceNotes { get; private set; }

    public string? DefaultPaymentInstructions { get; private set; }

    public OrganizationLogo? Logo { get; private set; }

    public byte[] RowVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public static Organization CreateSingleton()
    {
        return new Organization
        {
            Id = SingletonId,
            LegalName = string.Empty,
            DisplayName = string.Empty,
            Address = PostalAddress.Empty,
            DefaultCurrency = CurrencyCode.Usd,
            DefaultPaymentTermsDays = 30,
            DefaultInvoicePrefix = DocumentPrefix.InvoiceDefault,
            DefaultEstimatePrefix = DocumentPrefix.EstimateDefault
        };
    }

    public void UpdateProfile(
        string legalName,
        string displayName,
        PostalAddress address,
        string? email,
        string? phone,
        string? website,
        string? taxIdentifier,
        CurrencyCode defaultCurrency,
        int defaultPaymentTermsDays,
        DocumentPrefix defaultInvoicePrefix,
        DocumentPrefix defaultEstimatePrefix,
        string? defaultInvoiceNotes,
        string? defaultPaymentInstructions)
    {
        ArgumentNullException.ThrowIfNull(address);

        LegalName = OrganizationText.Required(legalName, nameof(legalName), NameMaxLength);
        DisplayName = OrganizationText.Required(displayName, nameof(displayName), NameMaxLength);
        Address = address;
        Email = OrganizationText.Optional(email, nameof(email), EmailMaxLength);
        Phone = OrganizationText.Optional(phone, nameof(phone), PhoneMaxLength);
        Website = NormalizeWebsite(website);
        TaxIdentifier = OrganizationText.Optional(taxIdentifier, nameof(taxIdentifier), TaxIdentifierMaxLength);
        DefaultCurrency = defaultCurrency;
        DefaultPaymentTermsDays = NormalizePaymentTerms(defaultPaymentTermsDays);
        DefaultInvoicePrefix = defaultInvoicePrefix;
        DefaultEstimatePrefix = defaultEstimatePrefix;
        DefaultInvoiceNotes = OrganizationText.Optional(defaultInvoiceNotes, nameof(defaultInvoiceNotes), NotesMaxLength);
        DefaultPaymentInstructions = OrganizationText.Optional(
            defaultPaymentInstructions,
            nameof(defaultPaymentInstructions),
            NotesMaxLength);
    }

    public void SetLogo(OrganizationLogo logo)
    {
        ArgumentNullException.ThrowIfNull(logo);
        Logo = logo;
    }

    public void ClearLogo() => Logo = null;

    public void SetCreated(DateTimeOffset atUtc, Guid? byUserId)
    {
        CreatedAtUtc = atUtc;
        CreatedByUserId = byUserId;
    }

    public void SetUpdated(DateTimeOffset atUtc, Guid? byUserId)
    {
        UpdatedAtUtc = atUtc;
        UpdatedByUserId = byUserId;
    }

    private static int NormalizePaymentTerms(int days)
    {
        if (days is < MinPaymentTermsDays or > MaxPaymentTermsDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(days),
                $"Payment terms must be between {MinPaymentTermsDays} and {MaxPaymentTermsDays} days.");
        }

        return days;
    }

    private static string? NormalizeWebsite(string? website)
    {
        string? trimmed = OrganizationText.Optional(website, nameof(website), WebsiteMaxLength);
        if (trimmed is null)
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Website must be an http or https URL.", nameof(website));
        }

        return uri.AbsoluteUri;
    }
}

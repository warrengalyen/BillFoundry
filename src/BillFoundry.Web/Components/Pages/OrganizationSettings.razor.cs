using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Organizations;
using BillFoundry.Domain.Organizations;
using Microsoft.AspNetCore.Components.Forms;

namespace BillFoundry.Web.Components.Pages;

public partial class OrganizationSettings
{
    private static IReadOnlyList<string> SupportedCurrencies { get; } =
        [.. CurrencyCode.SupportedCodes.Order(StringComparer.Ordinal)];

    private IBrowserFile? _pendingLogo;

    private OrganizationInput Input { get; set; } = new();

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    private bool HasLogo { get; set; }

    private string? LogoCacheBust { get; set; }

    private string LogoUrl => $"/media/organization-logo?v={Uri.EscapeDataString(LogoCacheBust ?? string.Empty)}";

    protected override async Task OnInitializedAsync()
    {
        OrganizationSettingsResult result = await Settings.GetAsync();
        ApplyResult(result, successMessage: null);
    }

    private async Task SaveAsync()
    {
        ClearMessages();
        OrganizationSettingsResult result = await Settings.UpdateAsync(Input.ToCommand());
        ApplyResult(result, "Organization settings were saved.");
    }

    private void OnLogoSelected(InputFileChangeEventArgs args)
    {
        _pendingLogo = args.FileCount > 0 ? args.File : null;
    }

    private async Task UploadLogoAsync()
    {
        ClearMessages();
        if (_pendingLogo is null)
        {
            ErrorMessage = "Choose a logo file to upload.";
            return;
        }

        try
        {
            await using Stream stream = _pendingLogo.OpenReadStream(OrganizationLogoRules.MaxSizeBytes);
            OrganizationSettingsResult result = await Settings.UploadLogoAsync(stream, Input.RowVersionBytes);
            if (result.Succeeded)
            {
                _pendingLogo = null;
            }

            ApplyResult(result, "The organization logo was updated.");
        }
        catch (IOException)
        {
            ErrorMessage = $"The logo must be {OrganizationLogoRules.SizeLimitDescription} or smaller.";
        }
    }

    private async Task RemoveLogoAsync()
    {
        ClearMessages();
        OrganizationSettingsResult result = await Settings.RemoveLogoAsync(Input.RowVersionBytes);
        ApplyResult(result, "The organization logo was removed.");
    }

    private void ApplyResult(OrganizationSettingsResult result, string? successMessage)
    {
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.Organization is not null)
        {
            Input.CopyFrom(result.Organization);
            HasLogo = result.Organization.HasLogo;
            LogoCacheBust = Convert.ToBase64String(result.Organization.RowVersion);
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
            return;
        }

        Errors = [.. result.Errors];
        if (result.IsConcurrencyConflict)
        {
            ErrorMessage = result.Errors.Count > 0
                ? result.Errors[0]
                : "The organization was updated by another user. Review the current values and save again.";
            Errors = [];
        }
    }

    private void ClearMessages()
    {
        StatusMessage = null;
        ErrorMessage = null;
        Errors = [];
    }

    private sealed class OrganizationInput
    {
        [Required]
        [StringLength(Organization.NameMaxLength)]
        public string LegalName { get; set; } = string.Empty;

        [Required]
        [StringLength(Organization.NameMaxLength)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [StringLength(PostalAddress.LineMaxLength)]
        public string AddressLine1 { get; set; } = string.Empty;

        [StringLength(PostalAddress.LineMaxLength)]
        public string? AddressLine2 { get; set; }

        [Required]
        [StringLength(PostalAddress.CityMaxLength)]
        public string City { get; set; } = string.Empty;

        [StringLength(PostalAddress.RegionMaxLength)]
        public string? Region { get; set; }

        [StringLength(PostalAddress.PostalCodeMaxLength)]
        public string? PostalCode { get; set; }

        [Required]
        [StringLength(PostalAddress.CountryMaxLength)]
        public string Country { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(Organization.EmailMaxLength)]
        public string? Email { get; set; }

        [StringLength(Organization.PhoneMaxLength)]
        public string? Phone { get; set; }

        [StringLength(Organization.WebsiteMaxLength)]
        public string? Website { get; set; }

        [StringLength(Organization.TaxIdentifierMaxLength)]
        public string? TaxIdentifier { get; set; }

        [Required]
        public string DefaultCurrency { get; set; } = CurrencyCode.Usd.Value;

        [Range(Organization.MinPaymentTermsDays, Organization.MaxPaymentTermsDays)]
        public int DefaultPaymentTermsDays { get; set; } = 30;

        [Required]
        [StringLength(DocumentPrefix.MaxLength)]
        public string DefaultInvoicePrefix { get; set; } = DocumentPrefix.InvoiceDefault.Value;

        [Required]
        [StringLength(DocumentPrefix.MaxLength)]
        public string DefaultEstimatePrefix { get; set; } = DocumentPrefix.EstimateDefault.Value;

        [StringLength(Organization.NotesMaxLength)]
        public string? DefaultInvoiceNotes { get; set; }

        [StringLength(Organization.NotesMaxLength)]
        public string? DefaultPaymentInstructions { get; set; }

        public string RowVersionBase64 { get; set; } = string.Empty;

        public byte[] RowVersionBytes =>
            string.IsNullOrWhiteSpace(RowVersionBase64) ? [] : Convert.FromBase64String(RowVersionBase64);

        public void CopyFrom(OrganizationSettingsDto organization)
        {
            LegalName = organization.LegalName;
            DisplayName = organization.DisplayName;
            AddressLine1 = organization.AddressLine1;
            AddressLine2 = organization.AddressLine2;
            City = organization.City;
            Region = organization.Region;
            PostalCode = organization.PostalCode;
            Country = organization.Country;
            Email = organization.Email;
            Phone = organization.Phone;
            Website = organization.Website;
            TaxIdentifier = organization.TaxIdentifier;
            DefaultCurrency = organization.DefaultCurrency;
            DefaultPaymentTermsDays = organization.DefaultPaymentTermsDays;
            DefaultInvoicePrefix = organization.DefaultInvoicePrefix;
            DefaultEstimatePrefix = organization.DefaultEstimatePrefix;
            DefaultInvoiceNotes = organization.DefaultInvoiceNotes;
            DefaultPaymentInstructions = organization.DefaultPaymentInstructions;
            RowVersionBase64 = Convert.ToBase64String(organization.RowVersion);
        }

        public UpdateOrganizationCommand ToCommand() =>
            new()
            {
                LegalName = LegalName,
                DisplayName = DisplayName,
                AddressLine1 = AddressLine1,
                AddressLine2 = AddressLine2,
                City = City,
                Region = Region,
                PostalCode = PostalCode,
                Country = Country,
                Email = Email,
                Phone = Phone,
                Website = Website,
                TaxIdentifier = TaxIdentifier,
                DefaultCurrency = DefaultCurrency,
                DefaultPaymentTermsDays = DefaultPaymentTermsDays,
                DefaultInvoicePrefix = DefaultInvoicePrefix,
                DefaultEstimatePrefix = DefaultEstimatePrefix,
                DefaultInvoiceNotes = DefaultInvoiceNotes,
                DefaultPaymentInstructions = DefaultPaymentInstructions,
                RowVersion = RowVersionBytes
            };
    }
}

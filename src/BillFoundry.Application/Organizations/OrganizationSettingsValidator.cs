using System.Net.Mail;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Application.Organizations;

public static class OrganizationSettingsValidator
{
    public static IReadOnlyList<string> Validate(UpdateOrganizationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();

        Require(command.LegalName, "Legal name", Organization.NameMaxLength, errors);
        Require(command.DisplayName, "Display name", Organization.NameMaxLength, errors);
        Require(command.AddressLine1, "Mailing address", PostalAddress.LineMaxLength, errors);
        Optional(command.AddressLine2, "Address line 2", PostalAddress.LineMaxLength, errors);
        Require(command.City, "City", PostalAddress.CityMaxLength, errors);
        Optional(command.Region, "State or province", PostalAddress.RegionMaxLength, errors);
        Optional(command.PostalCode, "Postal code", PostalAddress.PostalCodeMaxLength, errors);
        Require(command.Country, "Country", PostalAddress.CountryMaxLength, errors);
        Optional(command.Phone, "Phone", Organization.PhoneMaxLength, errors);
        Optional(command.TaxIdentifier, "Tax identifier", Organization.TaxIdentifierMaxLength, errors);
        Optional(command.DefaultInvoiceNotes, "Default invoice notes", Organization.NotesMaxLength, errors);
        Optional(command.DefaultPaymentInstructions, "Default payment instructions", Organization.NotesMaxLength, errors);

        ValidateEmail(command.Email, errors);
        ValidateWebsite(command.Website, errors);

        if (!CurrencyCode.TryParse(command.DefaultCurrency, out _))
        {
            errors.Add("Default currency is not a supported ISO 4217 code.");
        }

        if (command.DefaultPaymentTermsDays is < Organization.MinPaymentTermsDays
            or > Organization.MaxPaymentTermsDays)
        {
            errors.Add(
                $"Default payment terms must be between {Organization.MinPaymentTermsDays} and {Organization.MaxPaymentTermsDays} days.");
        }

        if (!DocumentPrefix.TryCreate(command.DefaultInvoicePrefix, out _))
        {
            errors.Add("Invoice prefix must start with a letter and contain only letters and digits (maximum 10 characters).");
        }

        if (!DocumentPrefix.TryCreate(command.DefaultEstimatePrefix, out _))
        {
            errors.Add("Estimate prefix must start with a letter and contain only letters and digits (maximum 10 characters).");
        }

        if (command.RowVersion is not { Length: > 0 })
        {
            errors.Add("The organization version is missing. Reload the page and try again.");
        }

        return errors;
    }

    private static void Require(string? value, string label, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
            return;
        }

        Optional(value, label, maxLength, errors);
    }

    private static void Optional(string? value, string label, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            errors.Add($"{label} must be at most {maxLength} characters.");
        }

        if (trimmed.Any(char.IsControl))
        {
            errors.Add($"{label} cannot contain control characters.");
        }
    }

    private static void ValidateEmail(string? email, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        string trimmed = email.Trim();
        if (trimmed.Length > Organization.EmailMaxLength)
        {
            errors.Add($"Email must be at most {Organization.EmailMaxLength} characters.");
            return;
        }

        if (trimmed.Contains('<', StringComparison.Ordinal)
            || trimmed.Contains('>', StringComparison.Ordinal)
            || !MailAddress.TryCreate(trimmed, out MailAddress? parsed)
            || !string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Email is not valid.");
        }
    }

    private static void ValidateWebsite(string? website, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return;
        }

        string trimmed = website.Trim();
        if (trimmed.Length > Organization.WebsiteMaxLength)
        {
            errors.Add($"Website must be at most {Organization.WebsiteMaxLength} characters.");
            return;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            errors.Add("Website must be an http or https URL.");
        }
    }
}

using System.Net.Mail;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Application.Clients;

public static class ClientValidator
{
    public static IReadOnlyList<string> Validate(SaveClientCommand command, bool requireRowVersion)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(command.Code) && !ClientCode.TryCreate(command.Code, out _))
        {
            errors.Add("Client code must start with a letter or digit and may contain letters, digits, periods, underscores, or hyphens (maximum 20 characters).");
        }

        Require(command.Name, "Name", Client.NameMaxLength, errors);
        Optional(command.Phone, "Phone", Client.PhoneMaxLength, errors);
        Optional(command.Notes, "Notes", Client.NotesMaxLength, errors);
        ValidateEmail(command.Email, "Billing email", Client.EmailMaxLength, errors);
        ValidateWebsite(command.Website, errors);
        ValidateAddress(command, errors);

        if (requireRowVersion && command is UpdateClientCommand { RowVersion.Length: 0 })
        {
            errors.Add("The client version is missing. Reload the page and try again.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(SaveContactCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        Require(command.Name, "Contact name", ClientContact.NameMaxLength, errors);
        Optional(command.JobTitle, "Job title", ClientContact.JobTitleMaxLength, errors);
        Optional(command.Phone, "Phone", ClientContact.PhoneMaxLength, errors);
        ValidateEmail(command.Email, "Email", ClientContact.EmailMaxLength, errors);

        if (command.RowVersion is not { Length: > 0 })
        {
            errors.Add("The client version is missing. Reload the page and try again.");
        }

        return errors;
    }

    private static void ValidateAddress(SaveClientCommand command, List<string> errors)
    {
        bool anyAddress = HasValue(command.AddressLine1)
            || HasValue(command.AddressLine2)
            || HasValue(command.City)
            || HasValue(command.Region)
            || HasValue(command.PostalCode)
            || HasValue(command.Country);

        if (!anyAddress)
        {
            return;
        }

        Require(command.AddressLine1, "Address line 1", PostalAddress.LineMaxLength, errors);
        Optional(command.AddressLine2, "Address line 2", PostalAddress.LineMaxLength, errors);
        Require(command.City, "City", PostalAddress.CityMaxLength, errors);
        Optional(command.Region, "State or province", PostalAddress.RegionMaxLength, errors);
        Optional(command.PostalCode, "Postal code", PostalAddress.PostalCodeMaxLength, errors);
        Require(command.Country, "Country", PostalAddress.CountryMaxLength, errors);
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

    private static void ValidateEmail(string? email, string label, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        string trimmed = email.Trim();
        if (trimmed.Length > maxLength)
        {
            errors.Add($"{label} must be at most {maxLength} characters.");
            return;
        }

        if (trimmed.Contains('<', StringComparison.Ordinal)
            || trimmed.Contains('>', StringComparison.Ordinal)
            || !MailAddress.TryCreate(trimmed, out MailAddress? parsed)
            || !string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} is not valid.");
        }
    }

    private static void ValidateWebsite(string? website, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return;
        }

        string trimmed = website.Trim();
        if (trimmed.Length > Client.WebsiteMaxLength)
        {
            errors.Add($"Website must be at most {Client.WebsiteMaxLength} characters.");
            return;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            errors.Add("Website must be an http or https URL.");
        }
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}

namespace BillFoundry.Domain.Organizations;

internal static class OrganizationText
{
    public static string Required(string? value, string name, int maxLength)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        ValidateContent(trimmed, name, maxLength);
        return trimmed;
    }

    public static string? Optional(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        ValidateContent(trimmed, name, maxLength);
        return trimmed;
    }

    private static void ValidateContent(string value, string name, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{name} must be at most {maxLength} characters.", name);
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException($"{name} cannot contain control characters.", name);
        }
    }
}

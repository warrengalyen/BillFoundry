using BillFoundry.Domain.Catalog;

namespace BillFoundry.Application.Catalog;

public static class CatalogItemValidator
{
    public static IReadOnlyList<string> Validate(SaveCatalogItemCommand command, bool requireRowVersion)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        Require(command.Name, "Name", CatalogItem.NameMaxLength, errors);
        Optional(command.Description, "Description", CatalogItem.DescriptionMaxLength, errors);

        if (!string.IsNullOrWhiteSpace(command.Sku) && !CatalogSku.TryCreate(command.Sku, out _))
        {
            errors.Add("SKU must start with a letter or digit and may contain letters, digits, periods, underscores, or hyphens (maximum 40 characters).");
        }

        if (!CatalogUnitTypeDisplay.IsDefined(command.UnitType))
        {
            errors.Add("Unit type is not valid.");
        }

        if (command.DefaultUnitPrice < 0m)
        {
            errors.Add("Default unit price cannot be negative.");
        }
        else if (command.DefaultUnitPrice > CatalogItem.MaxUnitPrice)
        {
            errors.Add($"Default unit price cannot be greater than {CatalogItem.MaxUnitPrice}.");
        }
        else
        {
            decimal rounded = decimal.Round(command.DefaultUnitPrice, CatalogItem.PriceScale, MidpointRounding.AwayFromZero);
            if (rounded != command.DefaultUnitPrice)
            {
                errors.Add($"Default unit price cannot have more than {CatalogItem.PriceScale} decimal places.");
            }
        }

        if (requireRowVersion && command is UpdateCatalogItemCommand { RowVersion.Length: 0 })
        {
            errors.Add("The catalog item version is missing. Reload the page and try again.");
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
}

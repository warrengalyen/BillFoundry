using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Invoices;

namespace BillFoundry.Application.Invoices;

public static class InvoiceValidator
{
    public static IReadOnlyList<string> ValidateHeader(SaveInvoiceCommand command, bool requireRowVersion)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        if (command.ClientId == Guid.Empty)
        {
            errors.Add("Client is required.");
        }

        if (command.IssueDate == default)
        {
            errors.Add("Issue date is required.");
        }

        if (command.DueDate == default)
        {
            errors.Add("Due date is required.");
        }
        else if (command.IssueDate != default && command.DueDate < command.IssueDate)
        {
            errors.Add("Due date cannot be earlier than the issue date.");
        }

        Optional(command.PurchaseOrder, "Purchase order", Invoice.PurchaseOrderMaxLength, errors);
        Optional(command.Notes, "Notes", Invoice.NotesMaxLength, errors);
        Optional(command.PaymentInstructions, "Payment instructions", Invoice.PaymentInstructionsMaxLength, errors);
        ValidateAmount(command.Discount, "Discount", Invoice.MaxDiscount, errors);
        ValidateRate(command.TaxRatePercent, errors);

        if (requireRowVersion && command is UpdateInvoiceCommand { RowVersion.Length: 0 })
        {
            errors.Add("The invoice version is missing. Reload the page and try again.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateLine(SaveInvoiceLineCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        if (command.Id == Guid.Empty)
        {
            errors.Add("The invoice was not found.");
        }

        if (command.RowVersion is not { Length: > 0 })
        {
            errors.Add("The invoice version is missing. Reload the page and try again.");
        }

        Require(command.Description, "Description", InvoiceLine.DescriptionMaxLength, errors);

        if (command.Quantity <= 0m || command.Quantity > InvoiceLine.MaxQuantity)
        {
            errors.Add("Quantity must be greater than zero and at most 999,999.9999.");
        }
        else if (!MoneyRounding.HasQuantityScale(command.Quantity))
        {
            errors.Add("Quantity cannot have more than four decimal places.");
        }

        if (!CatalogUnitTypeDisplay.IsDefined(command.Unit))
        {
            errors.Add("Unit type is not valid.");
        }

        if (command.UnitPrice < 0m || command.UnitPrice > InvoiceLine.MaxUnitPrice)
        {
            errors.Add("Unit price cannot be negative or exceed the maximum.");
        }
        else if (!MoneyRounding.HasPriceScale(command.UnitPrice))
        {
            errors.Add("Unit price cannot have more than four decimal places.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateConcurrency(InvoiceConcurrencyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        if (command.Id == Guid.Empty)
        {
            errors.Add("The invoice was not found.");
        }

        if (command.RowVersion is not { Length: > 0 })
        {
            errors.Add("The invoice version is missing. Reload the page and try again.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateVoid(VoidInvoiceCommand command)
    {
        var errors = ValidateConcurrency(command).ToList();
        Require(command.Reason, "Void reason", Invoice.VoidReasonMaxLength, errors);
        return errors;
    }

    public static IReadOnlyList<string> ValidateConvert(ConvertEstimateCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        if (command.EstimateId == Guid.Empty)
        {
            errors.Add("The estimate was not found.");
        }

        if (command.EstimateRowVersion is not { Length: > 0 })
        {
            errors.Add("The estimate version is missing. Reload the page and try again.");
        }

        if (command.IssueDate is DateOnly issue
            && command.DueDate is DateOnly due
            && due < issue)
        {
            errors.Add("Due date cannot be earlier than the issue date.");
        }

        Optional(command.PurchaseOrder, "Purchase order", Invoice.PurchaseOrderMaxLength, errors);
        Optional(command.Notes, "Notes", Invoice.NotesMaxLength, errors);
        Optional(command.PaymentInstructions, "Payment instructions", Invoice.PaymentInstructionsMaxLength, errors);
        return errors;
    }

    private static void ValidateAmount(decimal value, string label, decimal max, List<string> errors)
    {
        if (value < 0m)
        {
            errors.Add($"{label} cannot be negative.");
            return;
        }

        if (value > max)
        {
            errors.Add($"{label} cannot exceed the maximum.");
            return;
        }

        if (!MoneyRounding.HasAmountScale(value))
        {
            errors.Add($"{label} cannot have more than two decimal places.");
        }
    }

    private static void ValidateRate(decimal taxRatePercent, List<string> errors)
    {
        if (taxRatePercent < 0m || taxRatePercent > Invoice.MaxTaxRatePercent)
        {
            errors.Add("Tax rate must be between 0 and 100 percent.");
            return;
        }

        if (!MoneyRounding.HasRateScale(taxRatePercent))
        {
            errors.Add("Tax rate cannot have more than four decimal places.");
        }
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

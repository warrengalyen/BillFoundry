using BillFoundry.Application.Invoices;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;

namespace BillFoundry.Application.Tests;

public sealed class InvoiceValidatorTests
{
    [Fact]
    public void ValidateHeader_accepts_a_minimal_draft()
    {
        IReadOnlyList<string> errors = InvoiceValidator.ValidateHeader(
            new SaveInvoiceCommand
            {
                ClientId = Guid.NewGuid(),
                IssueDate = new DateOnly(2026, 8, 22),
                DueDate = new DateOnly(2026, 9, 21)
            },
            requireRowVersion: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateHeader_requires_client_issue_and_due_dates()
    {
        IReadOnlyList<string> errors = InvoiceValidator.ValidateHeader(
            new SaveInvoiceCommand(),
            requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("Client", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Issue date", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Due date", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateHeader_rejects_due_before_issue_and_extra_decimals()
    {
        var command = new SaveInvoiceCommand
        {
            ClientId = Guid.NewGuid(),
            IssueDate = new DateOnly(2026, 8, 22),
            DueDate = new DateOnly(2026, 8, 21),
            Discount = 1.001m,
            TaxRatePercent = 8.12345m
        };

        IReadOnlyList<string> errors = InvoiceValidator.ValidateHeader(command, requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("Due date", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Discount", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Tax rate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_requires_description_and_positive_quantity()
    {
        IReadOnlyList<string> errors = InvoiceValidator.ValidateLine(new SaveInvoiceLineCommand
        {
            Id = Guid.NewGuid(),
            RowVersion = [1],
            Description = " ",
            Quantity = 0m,
            Unit = CatalogUnitType.Hour,
            UnitPrice = -1m
        });

        Assert.Contains(errors, error => error.Contains("Description", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Quantity", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Unit price", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateHeader_requires_row_version_for_updates()
    {
        IReadOnlyList<string> errors = InvoiceValidator.ValidateHeader(
            new UpdateInvoiceCommand
            {
                ClientId = Guid.NewGuid(),
                IssueDate = new DateOnly(2026, 8, 22),
                DueDate = new DateOnly(2026, 9, 21),
                RowVersion = []
            },
            requireRowVersion: true);

        Assert.Contains(errors, error => error.Contains("version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateVoid_requires_a_reason()
    {
        IReadOnlyList<string> errors = InvoiceValidator.ValidateVoid(new VoidInvoiceCommand
        {
            Id = Guid.NewGuid(),
            RowVersion = [1],
            Reason = " "
        });

        Assert.Contains(errors, error => error.Contains("Void reason", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateConvert_requires_estimate_identity_and_version()
    {
        IReadOnlyList<string> errors = InvoiceValidator.ValidateConvert(new ConvertEstimateCommand());

        Assert.Contains(errors, error => error.Contains("estimate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePayment_rejects_zero_negative_and_invalid_values()
    {
        IReadOnlyList<string> empty = InvoiceValidator.ValidatePayment(new RecordPaymentCommand());
        Assert.Contains(empty, error => error.Contains("invoice", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(empty, error => error.Contains("version", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(empty, error => error.Contains("Payment date", StringComparison.Ordinal));
        Assert.Contains(empty, error => error.Contains("greater than zero", StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<string> invalid = InvoiceValidator.ValidatePayment(new RecordPaymentCommand
        {
            Id = Guid.NewGuid(),
            RowVersion = [1],
            PaymentDate = new DateOnly(2026, 8, 22),
            Amount = -5m,
            Method = (PaymentMethod)99,
            Reference = new string('x', InvoicePayment.ReferenceMaxLength + 1)
        });
        Assert.Contains(invalid, error => error.Contains("greater than zero", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(invalid, error => error.Contains("method", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(invalid, error => error.Contains("Reference", StringComparison.Ordinal));

        IReadOnlyList<string> scale = InvoiceValidator.ValidatePayment(new RecordPaymentCommand
        {
            Id = Guid.NewGuid(),
            RowVersion = [1],
            PaymentDate = new DateOnly(2026, 8, 22),
            Amount = 1.001m,
            Method = PaymentMethod.Cash
        });
        Assert.Contains(scale, error => error.Contains("two decimal places", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateReverse_requires_payment_identity_and_reason()
    {
        IReadOnlyList<string> errors = InvoiceValidator.ValidateReverse(new ReversePaymentCommand
        {
            Id = Guid.NewGuid(),
            RowVersion = [1],
            Reason = " "
        });

        Assert.Contains(errors, error => error.Contains("payment", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Reversal reason", StringComparison.Ordinal));
    }
}

public sealed class InvoiceListQueryTests
{
    [Fact]
    public void Normalize_clamps_page_and_unknown_filters()
    {
        var query = new InvoiceListQuery
        {
            Search = "  INV-1  ",
            ClientId = Guid.Empty,
            Page = 0,
            PageSize = 500,
            SortBy = (InvoiceSortField)42,
            Status = (InvoiceStatusFilter)90,
            IssueFrom = default(DateOnly),
            MaxTotal = 1m,
            MinTotal = 5m
        };

        query.Normalize();

        Assert.Equal("INV-1", query.Search);
        Assert.Null(query.ClientId);
        Assert.Equal(1, query.Page);
        Assert.Equal(InvoiceListQuery.MaxPageSize, query.PageSize);
        Assert.Equal(InvoiceSortField.IssueDate, query.SortBy);
        Assert.Equal(InvoiceStatusFilter.All, query.Status);
        Assert.True(query.SortDescending);
        Assert.Null(query.IssueFrom);
        Assert.Equal(5m, query.MinTotal);
        Assert.Equal(5m, query.MaxTotal);
    }

    [Fact]
    public void Overdue_filter_uses_computed_status()
    {
        Assert.True(new InvoiceListQuery { Status = InvoiceStatusFilter.Overdue }.UsesComputedOverdue);
        Assert.True(new InvoiceListQuery { OverdueOnly = true }.UsesComputedOverdue);
        Assert.False(new InvoiceListQuery { Status = InvoiceStatusFilter.Sent }.UsesComputedOverdue);
    }
}

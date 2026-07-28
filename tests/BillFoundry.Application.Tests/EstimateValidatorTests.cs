using BillFoundry.Application.Estimates;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;

namespace BillFoundry.Application.Tests;

public sealed class EstimateValidatorTests
{
    [Fact]
    public void ValidateHeader_accepts_a_minimal_draft()
    {
        IReadOnlyList<string> errors = EstimateValidator.ValidateHeader(
            new SaveEstimateCommand
            {
                ClientId = Guid.NewGuid(),
                IssueDate = new DateOnly(2026, 8, 22)
            },
            requireRowVersion: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateHeader_requires_client_and_issue_date()
    {
        IReadOnlyList<string> errors = EstimateValidator.ValidateHeader(
            new SaveEstimateCommand(),
            requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("Client", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Issue date", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateHeader_rejects_expiration_before_issue_and_extra_decimals()
    {
        var command = new SaveEstimateCommand
        {
            ClientId = Guid.NewGuid(),
            IssueDate = new DateOnly(2026, 8, 22),
            ExpirationDate = new DateOnly(2026, 8, 21),
            Discount = 1.001m,
            TaxRatePercent = 8.12345m
        };

        IReadOnlyList<string> errors = EstimateValidator.ValidateHeader(command, requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("Expiration", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Discount", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Tax rate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_requires_description_and_positive_quantity()
    {
        IReadOnlyList<string> errors = EstimateValidator.ValidateLine(new SaveEstimateLineCommand
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
        IReadOnlyList<string> errors = EstimateValidator.ValidateHeader(
            new UpdateEstimateCommand
            {
                ClientId = Guid.NewGuid(),
                IssueDate = new DateOnly(2026, 8, 22),
                RowVersion = []
            },
            requireRowVersion: true);

        Assert.Contains(errors, error => error.Contains("version", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class EstimateListQueryTests
{
    [Fact]
    public void Normalize_clamps_page_and_unknown_filters()
    {
        var query = new EstimateListQuery
        {
            Search = "  EST-1  ",
            Page = 0,
            PageSize = 500,
            SortBy = (EstimateSortField)42,
            Status = (EstimateStatusFilter)90
        };

        query.Normalize();

        Assert.Equal("EST-1", query.Search);
        Assert.Equal(1, query.Page);
        Assert.Equal(EstimateListQuery.MaxPageSize, query.PageSize);
        Assert.Equal(EstimateSortField.IssueDate, query.SortBy);
        Assert.Equal(EstimateStatusFilter.All, query.Status);
        Assert.True(query.SortDescending);
        Assert.Null(query.StatusValue());
    }
}

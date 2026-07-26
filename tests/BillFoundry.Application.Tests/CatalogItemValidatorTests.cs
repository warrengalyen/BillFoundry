using BillFoundry.Application.Catalog;
using BillFoundry.Domain.Catalog;

namespace BillFoundry.Application.Tests;

public sealed class CatalogItemValidatorTests
{
    [Fact]
    public void Validate_accepts_a_minimal_item()
    {
        IReadOnlyList<string> errors = CatalogItemValidator.Validate(
            new SaveCatalogItemCommand { Name = "Design", UnitType = CatalogUnitType.Hour },
            requireRowVersion: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_requires_name()
    {
        IReadOnlyList<string> errors = CatalogItemValidator.Validate(
            new SaveCatalogItemCommand { Name = " ", UnitType = CatalogUnitType.Item },
            requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("Name", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rejects_invalid_sku_price_and_unit()
    {
        var command = new SaveCatalogItemCommand
        {
            Name = "Design",
            Sku = "bad sku",
            UnitType = (CatalogUnitType)99,
            DefaultUnitPrice = -5m
        };

        IReadOnlyList<string> errors = CatalogItemValidator.Validate(command, requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("SKU", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Unit type", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("negative", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_rejects_extra_decimal_places()
    {
        IReadOnlyList<string> errors = CatalogItemValidator.Validate(
            new SaveCatalogItemCommand
            {
                Name = "Design",
                UnitType = CatalogUnitType.Hour,
                DefaultUnitPrice = 1.12345m
            },
            requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("decimal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_requires_row_version_for_updates()
    {
        IReadOnlyList<string> errors = CatalogItemValidator.Validate(
            new UpdateCatalogItemCommand { Name = "Design", UnitType = CatalogUnitType.Hour, RowVersion = [] },
            requireRowVersion: true);

        Assert.Contains(errors, error => error.Contains("version", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class CatalogListQueryTests
{
    [Fact]
    public void Normalize_clamps_page_and_unknown_filters()
    {
        var query = new CatalogListQuery
        {
            Search = "  widget  ",
            Page = 0,
            PageSize = 500,
            SortBy = (CatalogSortField)42,
            Status = (CatalogStatusFilter)9,
            UnitType = (CatalogUnitTypeFilter)20
        };

        query.Normalize();

        Assert.Equal("widget", query.Search);
        Assert.Equal(1, query.Page);
        Assert.Equal(CatalogListQuery.MaxPageSize, query.PageSize);
        Assert.Equal(CatalogSortField.Name, query.SortBy);
        Assert.Equal(CatalogStatusFilter.Active, query.Status);
        Assert.Equal(CatalogUnitTypeFilter.All, query.UnitType);
    }
}

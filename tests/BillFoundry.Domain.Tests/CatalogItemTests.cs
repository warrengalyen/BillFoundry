using BillFoundry.Domain.Catalog;

namespace BillFoundry.Domain.Tests;

public sealed class CatalogSkuTests
{
    [Theory]
    [InlineData("web-001", "web-001")]
    [InlineData("A.B_1", "A.B_1")]
    [InlineData("  SKU1  ", "SKU1")]
    public void TryCreate_accepts_supported_skus(string input, string expected)
    {
        Assert.True(CatalogSku.TryCreate(input, out CatalogSku sku));
        Assert.Equal(expected, sku.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-ABC")]
    [InlineData("A sku")]
    [InlineData("THIS-SKU-IS-WAY-TOO-LONG-TO-BE-ACCEPTED-X")]
    public void TryCreate_rejects_invalid_skus(string? input)
    {
        Assert.False(CatalogSku.TryCreate(input, out _));
    }
}

public sealed class CatalogItemTests
{
    [Fact]
    public void Create_starts_active_with_normalized_fields()
    {
        CatalogItem item = CatalogItem.Create(
            "  Website design  ",
            "  Hourly design  ",
            " web-001 ",
            CatalogUnitType.Hour,
            125.50m,
            isTaxable: true);

        Assert.True(item.IsActive);
        Assert.Equal("Website design", item.Name);
        Assert.Equal("Hourly design", item.Description);
        Assert.Equal("web-001", item.Sku);
        Assert.Equal(CatalogUnitType.Hour, item.UnitType);
        Assert.Equal(125.50m, item.DefaultUnitPrice);
        Assert.True(item.IsTaxable);
    }

    [Fact]
    public void Create_allows_zero_price_and_omitted_sku()
    {
        CatalogItem item = CatalogItem.Create("Call", null, null, CatalogUnitType.FlatFee, 0m, isTaxable: false);

        Assert.Null(item.Sku);
        Assert.Equal(0m, item.DefaultUnitPrice);
        Assert.False(item.IsTaxable);
        Assert.Equal("Flat fee", CatalogUnitTypeDisplay.Label(item.UnitType));
    }

    [Fact]
    public void Deactivate_and_activate_toggle_status()
    {
        CatalogItem item = CatalogItem.Create("Widget", null, null, CatalogUnitType.Item, 10m, false);

        item.Deactivate();
        Assert.False(item.IsActive);

        item.Activate();
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Create_rejects_negative_price()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CatalogItem.Create("Bad", null, null, CatalogUnitType.Hour, -1m, false));
    }

    [Fact]
    public void Create_rejects_price_with_too_many_decimals()
    {
        Assert.Throws<ArgumentException>(() =>
            CatalogItem.Create("Bad", null, null, CatalogUnitType.Hour, 1.12345m, false));
    }

    [Fact]
    public void Create_rejects_undefined_unit_type()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CatalogItem.Create("Bad", null, null, (CatalogUnitType)42, 1m, false));
    }

    [Fact]
    public void Update_replaces_profile_fields()
    {
        CatalogItem item = CatalogItem.Create("Old", null, null, CatalogUnitType.Hour, 50m, false);

        item.Update("New", "Desc", "SKU-2", CatalogUnitType.Day, 800m, true);

        Assert.Equal("New", item.Name);
        Assert.Equal("Desc", item.Description);
        Assert.Equal("SKU-2", item.Sku);
        Assert.Equal(CatalogUnitType.Day, item.UnitType);
        Assert.Equal(800m, item.DefaultUnitPrice);
        Assert.True(item.IsTaxable);
    }
}

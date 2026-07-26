using BillFoundry.Application.Catalog;
using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class CatalogPersistenceTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public CatalogPersistenceTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Cat-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Create_persists_price_unit_and_sku()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser());
        ICatalogService service = provider.GetRequiredService<ICatalogService>();

        CatalogItemResult created = await service.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Design",
            Description = "Hourly product design",
            Sku = $"D{_marker[..8]}",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 125.5m,
            IsTaxable = true
        });

        Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        Assert.Equal(125.5m, created.Item?.DefaultUnitPrice);
        Assert.Equal(CatalogUnitType.Hour, created.Item?.UnitType);
        Assert.True(created.Item?.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(created.Item?.CurrencyCode));

        CatalogItemResult reloaded = await service.GetAsync(created.Item!.Id);
        Assert.Equal($"{_marker} Design", reloaded.Item?.Name);
        Assert.Equal(created.Item.Sku, reloaded.Item?.Sku);
        Assert.Equal(125.5m, reloaded.Item?.DefaultUnitPrice);
    }

    [Fact]
    public async Task List_pages_and_filters_without_loading_the_full_set()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        ICatalogService service = provider.GetRequiredService<ICatalogService>();
        for (int index = 1; index <= 25; index++)
        {
            CatalogItemResult created = await service.CreateAsync(new SaveCatalogItemCommand
            {
                Name = $"{_marker} Item {index:D2}",
                UnitType = index % 2 == 0 ? CatalogUnitType.Item : CatalogUnitType.Hour,
                DefaultUnitPrice = index
            });
            Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        }

        CatalogListResult page = await service.ListAsync(new CatalogListQuery
        {
            Search = _marker,
            Status = CatalogStatusFilter.All,
            Page = 2,
            PageSize = 10,
            SortBy = CatalogSortField.Name
        });

        Assert.True(page.Succeeded);
        Assert.Equal(25, page.Page?.TotalCount);
        Assert.Equal(10, page.Page?.Items.Count);
        Assert.Equal(2, page.Page?.Page);
        Assert.DoesNotContain(page.Page!.Items, item => item.Name.EndsWith("01", StringComparison.Ordinal));

        CatalogListResult hours = await service.ListAsync(new CatalogListQuery
        {
            Search = _marker,
            Status = CatalogStatusFilter.All,
            UnitType = CatalogUnitTypeFilter.Hour,
            PageSize = 100
        });
        Assert.Equal(13, hours.Page?.TotalCount);
        Assert.All(hours.Page!.Items, item => Assert.Equal(CatalogUnitType.Hour, item.UnitType));
    }

    [Fact]
    public async Task List_filters_inactive_items_out_of_the_active_view()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        ICatalogService service = provider.GetRequiredService<ICatalogService>();
        CatalogItemResult created = await service.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Inactive",
            UnitType = CatalogUnitType.Day,
            DefaultUnitPrice = 700m
        });
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));

        CatalogItemResult deactivated = await service.DeactivateAsync(new CatalogConcurrencyCommand
        {
            Id = created.Item!.Id,
            RowVersion = created.Item.RowVersion
        });
        Assert.True(deactivated.Succeeded, string.Join("; ", deactivated.Errors));

        CatalogListResult active = await service.ListAsync(new CatalogListQuery { Search = _marker, Status = CatalogStatusFilter.Active });
        CatalogListResult inactive = await service.ListAsync(new CatalogListQuery { Search = _marker, Status = CatalogStatusFilter.Inactive });

        Assert.DoesNotContain(active.Page!.Items, item => item.Id == created.Item.Id);
        Assert.Contains(inactive.Page!.Items, item => item.Id == created.Item.Id && !item.IsActive);
    }

    [Fact]
    public async Task Duplicate_sku_is_rejected()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        ICatalogService service = provider.GetRequiredService<ICatalogService>();
        string sku = $"S{_marker[..8]}";

        CatalogItemResult first = await service.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} One",
            Sku = sku,
            UnitType = CatalogUnitType.Item,
            DefaultUnitPrice = 10m
        });
        CatalogItemResult second = await service.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Two",
            Sku = sku,
            UnitType = CatalogUnitType.Item,
            DefaultUnitPrice = 11m
        });

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));
        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, error => error.Contains("SKU", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_detects_stale_row_version_and_accepts_a_fresh_token()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        ICatalogService service = provider.GetRequiredService<ICatalogService>();
        CatalogItemResult created = await service.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Concurrent",
            UnitType = CatalogUnitType.FlatFee,
            DefaultUnitPrice = 400m
        });
        byte[] stale = created.Item!.RowVersion;

        CatalogItemResult first = await service.UpdateAsync(new UpdateCatalogItemCommand
        {
            Id = created.Item.Id,
            RowVersion = stale,
            Name = $"{_marker} First",
            UnitType = CatalogUnitType.FlatFee,
            DefaultUnitPrice = 400m
        });
        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        CatalogItemResult second = await service.UpdateAsync(new UpdateCatalogItemCommand
        {
            Id = created.Item.Id,
            RowVersion = stale,
            Name = $"{_marker} Second",
            UnitType = CatalogUnitType.FlatFee,
            DefaultUnitPrice = 400m
        });
        Assert.True(second.IsConcurrencyConflict);

        CatalogItemResult chained = await service.UpdateAsync(new UpdateCatalogItemCommand
        {
            Id = created.Item.Id,
            RowVersion = first.Item!.RowVersion,
            Name = $"{_marker} Chained",
            UnitType = CatalogUnitType.FlatFee,
            DefaultUnitPrice = 425.125m
        });
        Assert.True(chained.Succeeded, string.Join("; ", chained.Errors));
        Assert.Equal(425.125m, chained.Item?.DefaultUnitPrice);
    }

    [Fact]
    public async Task List_uses_the_organization_default_currency()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IOrganizationSettingsService settings = provider.GetRequiredService<IOrganizationSettingsService>();
        ICatalogService catalog = provider.GetRequiredService<ICatalogService>();

        OrganizationSettingsResult current = await settings.GetAsync();
        UpdateOrganizationCommand command = OrganizationTestHost.ValidCommand(current.Organization!.RowVersion);
        command.DefaultCurrency = "EUR";
        OrganizationSettingsResult saved = await settings.UpdateAsync(command);
        Assert.True(saved.Succeeded, string.Join("; ", saved.Errors));

        CatalogItemResult created = await catalog.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Euro",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 90m
        });
        Assert.Equal("EUR", created.Item?.CurrencyCode);

        CatalogListResult list = await catalog.ListAsync(new CatalogListQuery { Search = _marker, Status = CatalogStatusFilter.All });
        Assert.Equal("EUR", list.CurrencyCode);
    }

    [Fact]
    public async Task Database_rejects_a_negative_unit_price()
    {
        await using var db = new BillFoundryDbContext(new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(_sql.ConnectionString)
            .Options);

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        SqlException exception = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO CatalogItems (Id, Name, UnitType, DefaultUnitPrice, IsTaxable, IsActive, CreatedAtUtc)
VALUES ({Guid.NewGuid()}, N'{_marker} Negative', N'Hour', -1, 0, 1, {createdAt})");
        });

        Assert.Equal(547, exception.Number);
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_manage_the_catalog()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        ICatalogService service = provider.GetRequiredService<ICatalogService>();

        CatalogListResult list = await service.ListAsync(new CatalogListQuery());
        CatalogItemResult create = await service.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Denied",
            UnitType = CatalogUnitType.Item
        });

        Assert.True(list.IsForbidden);
        Assert.True(create.IsForbidden);
    }

    [Fact]
    public async Task Validation_errors_do_not_persist_an_item()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        ICatalogService service = provider.GetRequiredService<ICatalogService>();

        CatalogItemResult result = await service.CreateAsync(new SaveCatalogItemCommand
        {
            Name = " ",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = -2m
        });

        Assert.False(result.Succeeded);
        CatalogListResult list = await service.ListAsync(new CatalogListQuery
        {
            Search = _marker,
            Status = CatalogStatusFilter.All
        });
        Assert.Empty(list.Page!.Items);
    }
}

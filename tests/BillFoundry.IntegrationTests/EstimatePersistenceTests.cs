using BillFoundry.Application.Catalog;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Estimates;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class EstimatePersistenceTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public EstimatePersistenceTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Est-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Create_assigns_a_unique_number_and_persists_totals()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser());
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);

        EstimateResult created = await estimates.CreateAsync(Header(clientId, discount: 0m, tax: 10m));
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        Assert.Equal(EstimateStatus.Draft, created.Estimate?.Status);
        Assert.StartsWith("EST-", created.Estimate?.Number, StringComparison.Ordinal);
        Assert.Equal(0m, created.Estimate?.Total);

        EstimateResult withLine = await estimates.AddLineAsync(Line(created.Estimate!, "Design", 2m, 125m, true));
        Assert.True(withLine.Succeeded, string.Join("; ", withLine.Errors));
        Assert.Equal(250.00m, withLine.Estimate?.Subtotal);
        Assert.Equal(25.00m, withLine.Estimate?.Tax);
        Assert.Equal(275.00m, withLine.Estimate?.Total);

        EstimateResult reloaded = await estimates.GetAsync(created.Estimate!.Id);
        Assert.Equal(created.Estimate.Number, reloaded.Estimate?.Number);
        Assert.Equal(275.00m, reloaded.Estimate?.Total);
        Assert.Equal("Design", reloaded.Estimate?.Lines[0].Description);
    }

    [Fact]
    public async Task Catalog_price_changes_do_not_alter_saved_estimate_lines()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);
        ICatalogService catalog = provider.GetRequiredService<ICatalogService>();

        CatalogItemResult item = await catalog.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Hour",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 100m,
            IsTaxable = true
        });
        Assert.True(item.Succeeded, string.Join("; ", item.Errors));

        EstimateResult created = await estimates.CreateAsync(Header(clientId));
        EstimateResult lined = await estimates.AddLineAsync(new SaveEstimateLineCommand
        {
            Id = created.Estimate!.Id,
            RowVersion = created.Estimate.RowVersion,
            CatalogItemId = item.Item!.Id,
            Description = "Original snapshot",
            Quantity = 1m,
            Unit = CatalogUnitType.Hour,
            UnitPrice = 100m,
            IsTaxable = true
        });
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));

        CatalogItemResult updated = await catalog.UpdateAsync(new UpdateCatalogItemCommand
        {
            Id = item.Item.Id,
            RowVersion = item.Item.RowVersion,
            Name = $"{_marker} Hour",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 999m,
            IsTaxable = false
        });
        Assert.True(updated.Succeeded, string.Join("; ", updated.Errors));

        EstimateResult reloaded = await estimates.GetAsync(created.Estimate.Id);
        Assert.Equal(100m, reloaded.Estimate?.Lines[0].UnitPrice);
        Assert.Equal("Original snapshot", reloaded.Estimate?.Lines[0].Description);
        Assert.True(reloaded.Estimate?.Lines[0].IsTaxable);
        Assert.Equal(100m, reloaded.Estimate?.Total);
    }

    [Fact]
    public async Task List_pages_filters_and_searches_on_the_server()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);
        for (int index = 1; index <= 12; index++)
        {
            EstimateResult created = await estimates.CreateAsync(Header(clientId, notes: $"{_marker} note {index:D2}"));
            Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        }

        EstimateListResult page = await estimates.ListAsync(new EstimateListQuery
        {
            Search = _marker,
            Page = 2,
            PageSize = 5,
            SortBy = EstimateSortField.Number,
            SortDescending = false
        });

        Assert.True(page.Succeeded);
        Assert.Equal(12, page.Page?.TotalCount);
        Assert.Equal(5, page.Page?.Items.Count);
        Assert.Equal(2, page.Page?.Page);

        EstimateListResult drafts = await estimates.ListAsync(new EstimateListQuery
        {
            Search = _marker,
            Status = EstimateStatusFilter.Draft,
            PageSize = 100
        });
        Assert.Equal(12, drafts.Page?.TotalCount);
        Assert.All(drafts.Page!.Items, item => Assert.Equal(EstimateStatus.Draft, item.Status));
    }

    [Fact]
    public async Task Parallel_creates_allocate_distinct_numbers()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        Guid clientId;
        await using (AsyncServiceScope setup = provider.CreateAsyncScope())
        {
            (_, clientId) = await SeedClientAsync(setup.ServiceProvider);
        }

        EstimateResult[] results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            IEstimateService estimates = scope.ServiceProvider.GetRequiredService<IEstimateService>();
            return await estimates.CreateAsync(Header(clientId, notes: _marker));
        }));

        Assert.All(results, result => Assert.True(result.Succeeded, string.Join("; ", result.Errors)));
        string[] numbers = [.. results.Select(result => result.Estimate!.Number)];
        Assert.Equal(numbers.Length, numbers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(numbers.Length, numbers.Select(number => number.Split('-')[1]).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Status_transitions_and_edit_guard_are_enforced()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);
        EstimateResult created = await estimates.CreateAsync(Header(clientId));
        EstimateResult sentEmpty = await estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = created.Estimate!.Id,
            RowVersion = created.Estimate.RowVersion,
            Target = EstimateStatus.Sent
        });
        Assert.False(sentEmpty.Succeeded);

        EstimateResult lined = await estimates.AddLineAsync(Line(created.Estimate, "Work", 1m, 50m, false));
        EstimateResult sent = await estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = lined.Estimate!.Id,
            RowVersion = lined.Estimate.RowVersion,
            Target = EstimateStatus.Sent
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));
        Assert.Equal(EstimateStatus.Sent, sent.Estimate?.Status);
        Assert.False(sent.Estimate?.CanEdit);

        EstimateResult editSent = await estimates.UpdateHeaderAsync(new UpdateEstimateCommand
        {
            Id = sent.Estimate!.Id,
            RowVersion = sent.Estimate.RowVersion,
            ClientId = clientId,
            IssueDate = sent.Estimate.IssueDate,
            Notes = "should fail"
        });
        Assert.False(editSent.Succeeded);

        EstimateResult accepted = await estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = sent.Estimate.Id,
            RowVersion = sent.Estimate.RowVersion,
            Target = EstimateStatus.Accepted
        });
        Assert.True(accepted.Succeeded, string.Join("; ", accepted.Errors));

        EstimateResult convert = await estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = accepted.Estimate!.Id,
            RowVersion = accepted.Estimate.RowVersion,
            Target = EstimateStatus.Converted
        });
        Assert.False(convert.Succeeded);
        Assert.Contains(convert.Errors, error => error.Contains("conversion", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(EstimateStatus.Accepted, (await estimates.GetAsync(accepted.Estimate.Id)).Estimate?.Status);
    }

    [Fact]
    public async Task Duplicate_copies_snapshots_and_allocates_a_new_number()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);
        EstimateResult created = await estimates.CreateAsync(Header(clientId, notes: $"{_marker} source", tax: 5m));
        EstimateResult lined = await estimates.AddLineAsync(Line(created.Estimate!, "Snapshot", 3m, 10m, true));

        EstimateResult copy = await estimates.DuplicateAsync(new DuplicateEstimateCommand { Id = lined.Estimate!.Id });
        Assert.True(copy.Succeeded, string.Join("; ", copy.Errors));
        Assert.NotEqual(lined.Estimate.Id, copy.Estimate?.Id);
        Assert.NotEqual(lined.Estimate.Number, copy.Estimate?.Number);
        Assert.Equal(EstimateStatus.Draft, copy.Estimate?.Status);
        Assert.Equal("Snapshot", copy.Estimate?.Lines[0].Description);
        Assert.Equal(10m, copy.Estimate?.Lines[0].UnitPrice);
        Assert.Equal(lined.Estimate.Total, copy.Estimate?.Total);
    }

    [Fact]
    public async Task Update_detects_stale_row_version()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);
        EstimateResult created = await estimates.CreateAsync(Header(clientId));
        byte[] stale = created.Estimate!.RowVersion;

        EstimateResult first = await estimates.UpdateHeaderAsync(new UpdateEstimateCommand
        {
            Id = created.Estimate.Id,
            RowVersion = stale,
            ClientId = clientId,
            IssueDate = created.Estimate.IssueDate,
            Notes = $"{_marker} first"
        });
        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        EstimateResult second = await estimates.UpdateHeaderAsync(new UpdateEstimateCommand
        {
            Id = created.Estimate.Id,
            RowVersion = stale,
            ClientId = clientId,
            IssueDate = created.Estimate.IssueDate,
            Notes = $"{_marker} second"
        });
        Assert.True(second.IsConcurrencyConflict);

        EstimateResult chained = await estimates.UpdateHeaderAsync(new UpdateEstimateCommand
        {
            Id = created.Estimate.Id,
            RowVersion = first.Estimate!.RowVersion,
            ClientId = clientId,
            IssueDate = created.Estimate.IssueDate,
            Notes = $"{_marker} chained"
        });
        Assert.True(chained.Succeeded, string.Join("; ", chained.Errors));
        Assert.Equal($"{_marker} chained", chained.Estimate?.Notes);
    }

    [Fact]
    public async Task Reorder_and_remove_lines_persist()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);
        EstimateResult created = await estimates.CreateAsync(Header(clientId));
        EstimateResult first = await estimates.AddLineAsync(Line(created.Estimate!, "First", 1m, 1m, false));
        EstimateResult second = await estimates.AddLineAsync(Line(first.Estimate!, "Second", 1m, 2m, false));

        Guid firstId = second.Estimate!.Lines.Single(line => line.Description == "First").Id;
        Guid secondId = second.Estimate.Lines.Single(line => line.Description == "Second").Id;

        EstimateResult reordered = await estimates.ReorderLinesAsync(new ReorderEstimateLinesCommand
        {
            Id = second.Estimate.Id,
            RowVersion = second.Estimate.RowVersion,
            LineIds = [secondId, firstId]
        });
        Assert.True(reordered.Succeeded, string.Join("; ", reordered.Errors));
        Assert.Equal(["Second", "First"], reordered.Estimate!.Lines.Select(line => line.Description).ToArray());

        EstimateResult removed = await estimates.RemoveLineAsync(new RemoveEstimateLineCommand
        {
            Id = reordered.Estimate.Id,
            LineId = secondId,
            RowVersion = reordered.Estimate.RowVersion
        });
        Assert.True(removed.Succeeded, string.Join("; ", removed.Errors));
        Assert.Single(removed.Estimate!.Lines);
        Assert.Equal("First", removed.Estimate.Lines[0].Description);
        Assert.Equal(0, removed.Estimate.Lines[0].SortOrder);
    }

    [Fact]
    public async Task Inactive_client_cannot_receive_a_new_estimate()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService clients = provider.GetRequiredService<IClientService>();
        IEstimateService estimates = provider.GetRequiredService<IEstimateService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Inactive" });
        ClientResult deactivated = await clients.DeactivateAsync(new ClientConcurrencyCommand
        {
            Id = client.Client!.Id,
            RowVersion = client.Client.RowVersion
        });
        Assert.True(deactivated.Succeeded, string.Join("; ", deactivated.Errors));

        EstimateResult created = await estimates.CreateAsync(Header(client.Client.Id));
        Assert.False(created.Succeeded);
        Assert.Contains(created.Errors, error => error.Contains("active", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_manage_estimates()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        IEstimateService service = provider.GetRequiredService<IEstimateService>();

        EstimateListResult list = await service.ListAsync(new EstimateListQuery());
        EstimateResult create = await service.CreateAsync(Header(Guid.NewGuid()));

        Assert.True(list.IsForbidden);
        Assert.True(create.IsForbidden);
    }

    [Fact]
    public async Task Database_rejects_an_unknown_status()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);
        EstimateResult created = await estimates.CreateAsync(Header(clientId));
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));

        await using var db = new BillFoundryDbContext(new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(_sql.ConnectionString)
            .Options);

        SqlException exception = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Estimates SET Status = N'Bogus' WHERE Id = {created.Estimate!.Id}");
        });

        Assert.Equal(547, exception.Number);
    }

    private async Task<(IEstimateService Estimates, Guid ClientId)> SeedClientAsync(IServiceProvider provider)
    {
        IClientService clients = provider.GetRequiredService<IClientService>();
        IEstimateService estimates = provider.GetRequiredService<IEstimateService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Client" });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        return (estimates, client.Client!.Id);
    }

    private SaveEstimateCommand Header(Guid clientId, decimal discount = 0m, decimal tax = 0m, string? notes = null) =>
        new()
        {
            ClientId = clientId,
            IssueDate = new DateOnly(2026, 8, 22),
            Discount = discount,
            TaxRatePercent = tax,
            Notes = notes ?? _marker
        };

    private static SaveEstimateLineCommand Line(
        EstimateDetailsDto estimate,
        string description,
        decimal quantity,
        decimal unitPrice,
        bool taxable) =>
        new()
        {
            Id = estimate.Id,
            RowVersion = estimate.RowVersion,
            Description = description,
            Quantity = quantity,
            Unit = CatalogUnitType.Hour,
            UnitPrice = unitPrice,
            IsTaxable = taxable
        };
}

using BillFoundry.Application.Catalog;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Estimates;
using BillFoundry.Application.Invoices;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Documents;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Invoices;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class InvoicePersistenceTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public InvoicePersistenceTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Inv-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Create_assigns_a_unique_number_and_persists_totals()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser());
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);

        InvoiceResult created = await invoices.CreateAsync(Header(clientId, discount: 0m, tax: 10m));
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        Assert.Equal(InvoiceStatus.Draft, created.Invoice?.Status);
        Assert.StartsWith("INV-", created.Invoice?.Number, StringComparison.Ordinal);
        Assert.Equal(0m, created.Invoice?.Total);
        Assert.Equal(0m, created.Invoice?.AmountPaid);
        Assert.Equal(0m, created.Invoice?.BalanceDue);

        InvoiceResult withLine = await invoices.AddLineAsync(Line(created.Invoice!, "Design", 2m, 125m, true));
        Assert.True(withLine.Succeeded, string.Join("; ", withLine.Errors));
        Assert.Equal(250.00m, withLine.Invoice?.Subtotal);
        Assert.Equal(25.00m, withLine.Invoice?.Tax);
        Assert.Equal(275.00m, withLine.Invoice?.Total);
        Assert.Equal(275.00m, withLine.Invoice?.BalanceDue);

        InvoiceResult reloaded = await invoices.GetAsync(created.Invoice!.Id);
        Assert.Equal(created.Invoice.Number, reloaded.Invoice?.Number);
        Assert.Equal(275.00m, reloaded.Invoice?.Total);
        Assert.Equal("Design", reloaded.Invoice?.Lines[0].Description);
    }

    [Fact]
    public async Task Catalog_and_client_changes_do_not_alter_saved_invoice_snapshots()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        ICatalogService catalog = provider.GetRequiredService<ICatalogService>();
        IClientService clients = provider.GetRequiredService<IClientService>();

        CatalogItemResult item = await catalog.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Hour",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 100m,
            IsTaxable = true
        });
        Assert.True(item.Succeeded, string.Join("; ", item.Errors));

        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        string originalClientName = created.Invoice!.ClientName;
        InvoiceResult lined = await invoices.AddLineAsync(new SaveInvoiceLineCommand
        {
            Id = created.Invoice.Id,
            RowVersion = created.Invoice.RowVersion,
            CatalogItemId = item.Item!.Id,
            Description = "Original snapshot",
            Quantity = 1m,
            Unit = CatalogUnitType.Hour,
            UnitPrice = 100m,
            IsTaxable = true
        });
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));

        CatalogItemResult updatedItem = await catalog.UpdateAsync(new UpdateCatalogItemCommand
        {
            Id = item.Item.Id,
            RowVersion = item.Item.RowVersion,
            Name = $"{_marker} Hour",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 999m,
            IsTaxable = false
        });
        Assert.True(updatedItem.Succeeded, string.Join("; ", updatedItem.Errors));

        ClientResult client = await clients.GetAsync(clientId);
        ClientResult renamed = await clients.UpdateAsync(new UpdateClientCommand
        {
            Id = clientId,
            RowVersion = client.Client!.RowVersion,
            Name = $"{_marker} Renamed"
        });
        Assert.True(renamed.Succeeded, string.Join("; ", renamed.Errors));

        InvoiceResult reloaded = await invoices.GetAsync(created.Invoice.Id);
        Assert.Equal(100m, reloaded.Invoice?.Lines[0].UnitPrice);
        Assert.Equal("Original snapshot", reloaded.Invoice?.Lines[0].Description);
        Assert.True(reloaded.Invoice?.Lines[0].IsTaxable);
        Assert.Equal(originalClientName, reloaded.Invoice?.ClientName);
        Assert.NotEqual($"{_marker} Renamed", reloaded.Invoice?.ClientName);
    }

    [Fact]
    public async Task List_pages_and_filters_on_the_server()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService clients = provider.GetRequiredService<IClientService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        ClientResult firstClient = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Alpha" });
        ClientResult secondClient = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Beta" });
        Assert.True(firstClient.Succeeded);
        Assert.True(secondClient.Succeeded);

        for (int index = 1; index <= 8; index++)
        {
            InvoiceResult created = await invoices.CreateAsync(Header(
                firstClient.Client!.Id,
                notes: $"{_marker} note {index:D2}",
                issue: new DateOnly(2026, 8, index),
                due: new DateOnly(2026, 9, index)));
            Assert.True(created.Succeeded, string.Join("; ", created.Errors));
            InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, "Work", 1m, 10m * index, false));
            Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));
        }

        InvoiceResult other = await invoices.CreateAsync(Header(secondClient.Client!.Id, notes: $"{_marker} other"));
        Assert.True(other.Succeeded, string.Join("; ", other.Errors));

        InvoiceListResult page = await invoices.ListAsync(new InvoiceListQuery
        {
            Search = _marker,
            ClientId = firstClient.Client!.Id,
            Page = 2,
            PageSize = 3,
            SortBy = InvoiceSortField.Number,
            SortDescending = false
        });

        Assert.True(page.Succeeded);
        Assert.Equal(8, page.Page?.TotalCount);
        Assert.Equal(3, page.Page?.Items.Count);

        InvoiceListResult amount = await invoices.ListAsync(new InvoiceListQuery
        {
            Search = _marker,
            MinTotal = 50m,
            MaxTotal = 80m,
            PageSize = 100
        });
        Assert.All(amount.Page!.Items, item =>
        {
            Assert.InRange(item.Total, 50m, 80m);
        });

        InvoiceListResult issued = await invoices.ListAsync(new InvoiceListQuery
        {
            Search = _marker,
            IssueFrom = new DateOnly(2026, 8, 3),
            IssueTo = new DateOnly(2026, 8, 5),
            PageSize = 100
        });
        Assert.Equal(3, issued.Page?.TotalCount);

        InvoiceListResult due = await invoices.ListAsync(new InvoiceListQuery
        {
            Search = _marker,
            DueFrom = new DateOnly(2026, 9, 6),
            DueTo = new DateOnly(2026, 9, 8),
            PageSize = 100
        });
        Assert.Equal(3, due.Page?.TotalCount);
        Assert.All(due.Page!.Items, item => Assert.Equal(firstClient.Client.Id, item.ClientId));
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

        InvoiceResult[] results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            IInvoiceService invoices = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            return await invoices.CreateAsync(Header(clientId, notes: _marker));
        }));

        Assert.All(results, result => Assert.True(result.Succeeded, string.Join("; ", result.Errors)));
        string[] numbers = [.. results.Select(result => result.Invoice!.Number)];
        Assert.Equal(numbers.Length, numbers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(numbers.Length, numbers.Select(number => number.Split('-')[1]).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Send_void_and_edit_guards_are_enforced()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        InvoiceResult sentEmpty = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = created.Invoice!.Id,
            RowVersion = created.Invoice.RowVersion
        });
        Assert.False(sentEmpty.Succeeded);

        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice, "Work", 1m, 50m, false));
        InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = lined.Invoice!.Id,
            RowVersion = lined.Invoice.RowVersion
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));
        Assert.Equal(InvoiceStatus.Sent, sent.Invoice?.Status);
        Assert.False(sent.Invoice?.CanEdit);

        InvoiceResult editSent = await invoices.UpdateHeaderAsync(new UpdateInvoiceCommand
        {
            Id = sent.Invoice!.Id,
            RowVersion = sent.Invoice.RowVersion,
            ClientId = clientId,
            IssueDate = sent.Invoice.IssueDate,
            DueDate = sent.Invoice.DueDate,
            Notes = "should fail"
        });
        Assert.False(editSent.Succeeded);

        InvoiceResult voided = await invoices.VoidAsync(new VoidInvoiceCommand
        {
            Id = sent.Invoice.Id,
            RowVersion = sent.Invoice.RowVersion,
            Reason = "Duplicate billing"
        });
        Assert.True(voided.Succeeded, string.Join("; ", voided.Errors));
        Assert.Equal(InvoiceStatus.Void, voided.Invoice?.Status);
        Assert.Equal(0m, voided.Invoice?.BalanceDue);
        Assert.Equal("Duplicate billing", voided.Invoice?.VoidReason);
        Assert.False(voided.Invoice?.CanVoid);
    }

    [Fact]
    public async Task Overdue_listing_uses_time_provider_and_does_not_persist_overdue()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);

        InvoiceResult created = await invoices.CreateAsync(Header(
            clientId,
            notes: $"{_marker} overdue",
            issue: new DateOnly(2026, 8, 1),
            due: new DateOnly(2026, 8, 10)));
        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, "Work", 1m, 75m, false));
        InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = lined.Invoice!.Id,
            RowVersion = lined.Invoice.RowVersion
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));
        Assert.Equal(InvoiceStatus.Sent, sent.Invoice?.Status);
        Assert.Equal(InvoiceStatus.Overdue, sent.Invoice?.EffectiveStatus);

        InvoiceListResult overdue = await invoices.ListAsync(new InvoiceListQuery
        {
            Search = _marker,
            Status = InvoiceStatusFilter.Overdue,
            PageSize = 100
        });
        Assert.Contains(overdue.Page!.Items, item => item.Id == sent.Invoice!.Id);
        Assert.All(overdue.Page.Items, item => Assert.Equal(InvoiceStatus.Overdue, item.EffectiveStatus));
        Assert.All(overdue.Page.Items, item => Assert.Equal(InvoiceStatus.Sent, item.Status));

        InvoiceListResult sentOnly = await invoices.ListAsync(new InvoiceListQuery
        {
            Search = _marker,
            Status = InvoiceStatusFilter.Sent,
            PageSize = 100
        });
        Assert.DoesNotContain(sentOnly.Page!.Items, item => item.Id == sent.Invoice!.Id);
    }

    [Fact]
    public async Task Duplicate_copies_snapshots_and_allocates_a_new_number()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        InvoiceResult created = await invoices.CreateAsync(Header(clientId, notes: $"{_marker} source", tax: 5m));
        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, "Snapshot", 3m, 10m, true));

        InvoiceResult copy = await invoices.DuplicateAsync(new DuplicateInvoiceCommand { Id = lined.Invoice!.Id });
        Assert.True(copy.Succeeded, string.Join("; ", copy.Errors));
        Assert.NotEqual(lined.Invoice.Id, copy.Invoice?.Id);
        Assert.NotEqual(lined.Invoice.Number, copy.Invoice?.Number);
        Assert.Equal(InvoiceStatus.Draft, copy.Invoice?.Status);
        Assert.Equal("Snapshot", copy.Invoice?.Lines[0].Description);
        Assert.Equal(10m, copy.Invoice?.Lines[0].UnitPrice);
        Assert.Equal(lined.Invoice.Total, copy.Invoice?.Total);
        Assert.Null(copy.Invoice?.SourceEstimateId);
    }

    [Fact]
    public async Task Convert_from_accepted_estimate_copies_financials_and_marks_converted()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, IEstimateService estimates, Guid clientId) = await SeedClientWithEstimatesAsync(provider);
        EstimateDetailsDto estimate = await AcceptedEstimateAsync(estimates, clientId, "Convert me", 2m, 125m, 10m, 8.25m);

        InvoiceResult converted = await invoices.ConvertFromEstimateAsync(new ConvertEstimateCommand
        {
            EstimateId = estimate.Id,
            EstimateRowVersion = estimate.RowVersion,
            PurchaseOrder = "PO-42"
        });

        Assert.True(converted.Succeeded, string.Join("; ", converted.Errors));
        Assert.Equal(InvoiceStatus.Draft, converted.Invoice?.Status);
        Assert.Equal(estimate.Id, converted.Invoice?.SourceEstimateId);
        Assert.Equal(estimate.Total, converted.Invoice?.Total);
        Assert.Equal(estimate.Discount, converted.Invoice?.Discount);
        Assert.Equal(estimate.TaxRatePercent, converted.Invoice?.TaxRatePercent);
        Assert.Equal("Convert me", converted.Invoice?.Lines[0].Description);
        Assert.Equal(125m, converted.Invoice?.Lines[0].UnitPrice);
        Assert.Equal("PO-42", converted.Invoice?.PurchaseOrder);
        Assert.Equal(converted.Invoice?.Total, converted.Invoice?.BalanceDue);

        EstimateResult after = await estimates.GetAsync(estimate.Id);
        Assert.Equal(EstimateStatus.Converted, after.Estimate?.Status);
        Assert.False(after.Estimate?.CanConvert);
        Assert.Equal(converted.Invoice?.Id, after.Estimate?.ConvertedInvoiceId);
        Assert.False(after.Estimate?.CanEdit);
    }

    [Fact]
    public async Task Convert_rejects_a_second_attempt()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, IEstimateService estimates, Guid clientId) = await SeedClientWithEstimatesAsync(provider);
        EstimateDetailsDto estimate = await AcceptedEstimateAsync(estimates, clientId, "Once", 1m, 50m);

        InvoiceResult first = await invoices.ConvertFromEstimateAsync(new ConvertEstimateCommand
        {
            EstimateId = estimate.Id,
            EstimateRowVersion = estimate.RowVersion
        });
        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        EstimateResult converted = await estimates.GetAsync(estimate.Id);
        InvoiceResult second = await invoices.ConvertFromEstimateAsync(new ConvertEstimateCommand
        {
            EstimateId = estimate.Id,
            EstimateRowVersion = converted.Estimate!.RowVersion
        });
        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, error => error.Contains("already been converted", StringComparison.OrdinalIgnoreCase));
        Assert.Single((await invoices.ListAsync(new InvoiceListQuery
        {
            Search = first.Invoice!.Number,
            PageSize = 10
        })).Page!.Items);
    }

    [Fact]
    public async Task Failed_conversion_rolls_back_sequence_and_leaves_the_estimate_accepted()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, IEstimateService estimates, Guid clientId) = await SeedClientWithEstimatesAsync(provider);
        EstimateDetailsDto estimate = await AcceptedEstimateAsync(estimates, clientId, "Rollback", 1m, 40m);
        int nextBefore = await InvoiceNextValueAsync();

        InvoiceResult failed = await invoices.ConvertFromEstimateAsync(new ConvertEstimateCommand
        {
            EstimateId = estimate.Id,
            EstimateRowVersion = [1, 2, 3, 4, 5, 6, 7, 8]
        });

        Assert.False(failed.Succeeded);
        Assert.Equal(nextBefore, await InvoiceNextValueAsync());
        EstimateResult after = await estimates.GetAsync(estimate.Id);
        Assert.Equal(EstimateStatus.Accepted, after.Estimate?.Status);
        Assert.True(after.Estimate?.CanConvert);
        Assert.Null(after.Estimate?.ConvertedInvoiceId);
    }

    [Fact]
    public async Task Update_detects_stale_row_version()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        byte[] stale = created.Invoice!.RowVersion;

        InvoiceResult first = await invoices.UpdateHeaderAsync(new UpdateInvoiceCommand
        {
            Id = created.Invoice.Id,
            RowVersion = stale,
            ClientId = clientId,
            IssueDate = created.Invoice.IssueDate,
            DueDate = created.Invoice.DueDate,
            Notes = $"{_marker} first"
        });
        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        InvoiceResult second = await invoices.UpdateHeaderAsync(new UpdateInvoiceCommand
        {
            Id = created.Invoice.Id,
            RowVersion = stale,
            ClientId = clientId,
            IssueDate = created.Invoice.IssueDate,
            DueDate = created.Invoice.DueDate,
            Notes = $"{_marker} second"
        });
        Assert.True(second.IsConcurrencyConflict);

        InvoiceResult chained = await invoices.UpdateHeaderAsync(new UpdateInvoiceCommand
        {
            Id = created.Invoice.Id,
            RowVersion = first.Invoice!.RowVersion,
            ClientId = clientId,
            IssueDate = created.Invoice.IssueDate,
            DueDate = created.Invoice.DueDate,
            Notes = $"{_marker} chained"
        });
        Assert.True(chained.Succeeded, string.Join("; ", chained.Errors));
        Assert.Equal($"{_marker} chained", chained.Invoice?.Notes);
    }

    [Fact]
    public async Task Inactive_client_cannot_receive_a_new_invoice()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService clients = provider.GetRequiredService<IClientService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Inactive" });
        ClientResult deactivated = await clients.DeactivateAsync(new ClientConcurrencyCommand
        {
            Id = client.Client!.Id,
            RowVersion = client.Client.RowVersion
        });
        Assert.True(deactivated.Succeeded, string.Join("; ", deactivated.Errors));

        InvoiceResult created = await invoices.CreateAsync(Header(client.Client.Id));
        Assert.False(created.Succeeded);
        Assert.Contains(created.Errors, error => error.Contains("active", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_manage_invoices()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        IInvoiceService service = provider.GetRequiredService<IInvoiceService>();

        InvoiceListResult list = await service.ListAsync(new InvoiceListQuery());
        InvoiceResult create = await service.CreateAsync(Header(Guid.NewGuid()));

        Assert.True(list.IsForbidden);
        Assert.True(create.IsForbidden);
    }

    [Fact]
    public async Task Database_rejects_an_unknown_status()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));

        await using var db = new BillFoundryDbContext(new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(_sql.ConnectionString)
            .Options);

        SqlException exception = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Invoices SET Status = N'Bogus' WHERE Id = {created.Invoice!.Id}");
        });

        Assert.Equal(547, exception.Number);
    }

    private async Task<(IInvoiceService Invoices, Guid ClientId)> SeedClientAsync(IServiceProvider provider)
    {
        IClientService clients = provider.GetRequiredService<IClientService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Client" });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        return (invoices, client.Client!.Id);
    }

    private async Task<(IInvoiceService Invoices, IEstimateService Estimates, Guid ClientId)> SeedClientWithEstimatesAsync(
        IServiceProvider provider)
    {
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        return (invoices, provider.GetRequiredService<IEstimateService>(), clientId);
    }

    private async Task<EstimateDetailsDto> AcceptedEstimateAsync(
        IEstimateService estimates,
        Guid clientId,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discount = 0m,
        decimal tax = 0m)
    {
        EstimateResult created = await estimates.CreateAsync(new SaveEstimateCommand
        {
            ClientId = clientId,
            IssueDate = new DateOnly(2026, 8, 1),
            Notes = _marker
        });
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        EstimateResult lined = await estimates.AddLineAsync(new SaveEstimateLineCommand
        {
            Id = created.Estimate!.Id,
            RowVersion = created.Estimate.RowVersion,
            Description = description,
            Quantity = quantity,
            Unit = CatalogUnitType.Hour,
            UnitPrice = unitPrice,
            IsTaxable = tax > 0m
        });
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));
        if (discount > 0m || tax > 0m)
        {
            EstimateResult headed = await estimates.UpdateHeaderAsync(new UpdateEstimateCommand
            {
                Id = lined.Estimate!.Id,
                RowVersion = lined.Estimate.RowVersion,
                ClientId = clientId,
                IssueDate = lined.Estimate.IssueDate,
                Notes = _marker,
                Discount = discount,
                TaxRatePercent = tax
            });
            Assert.True(headed.Succeeded, string.Join("; ", headed.Errors));
            lined = headed;
        }

        EstimateResult sent = await estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = lined.Estimate!.Id,
            RowVersion = lined.Estimate.RowVersion,
            Target = EstimateStatus.Sent
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));
        EstimateResult accepted = await estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = sent.Estimate!.Id,
            RowVersion = sent.Estimate.RowVersion,
            Target = EstimateStatus.Accepted
        });
        Assert.True(accepted.Succeeded, string.Join("; ", accepted.Errors));
        return accepted.Estimate!;
    }

    private SaveInvoiceCommand Header(
        Guid clientId,
        decimal discount = 0m,
        decimal tax = 0m,
        string? notes = null,
        DateOnly? issue = null,
        DateOnly? due = null)
    {
        DateOnly issued = issue ?? new DateOnly(2026, 8, 22);
        return new SaveInvoiceCommand
        {
            ClientId = clientId,
            IssueDate = issued,
            DueDate = due ?? issued.AddDays(30),
            Discount = discount,
            TaxRatePercent = tax,
            Notes = notes ?? _marker
        };
    }

    private static SaveInvoiceLineCommand Line(
        InvoiceDetailsDto invoice,
        string description,
        decimal quantity,
        decimal unitPrice,
        bool taxable) =>
        new()
        {
            Id = invoice.Id,
            RowVersion = invoice.RowVersion,
            Description = description,
            Quantity = quantity,
            Unit = CatalogUnitType.Hour,
            UnitPrice = unitPrice,
            IsTaxable = taxable
        };

    private async Task<int> InvoiceNextValueAsync()
    {
        await using var db = new BillFoundryDbContext(new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(_sql.ConnectionString)
            .Options);
        return await db.DocumentSequences
            .AsNoTracking()
            .Where(sequence => sequence.Kind == DocumentSequence.InvoiceKind)
            .Select(sequence => sequence.NextValue)
            .SingleAsync();
    }

    private sealed class FixedDateTimeProvider(DateOnly today) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}

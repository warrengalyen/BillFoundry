using System.Text;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Invoices;
using BillFoundry.Application.Reporting;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;
using BillFoundry.Infrastructure.Persistence;
using BillFoundry.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ReportingTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public ReportingTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Rpt-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Dashboard_aggregates_open_overdue_and_period_payments_in_sql()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        IReportingService reporting = provider.GetRequiredService<IReportingService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        ReportingResult<DashboardMetrics> beforeResult = await reporting.GetDashboardAsync();
        Assert.True(beforeResult.Succeeded, string.Join("; ", beforeResult.Errors));
        DashboardMetrics before = beforeResult.Value!;
        Guid alpha = await CreateClientAsync(provider, "Alpha Co");
        Guid beta = await CreateClientAsync(provider, "Beta LLC");

        InvoiceDetailsDto current = await SentInvoiceAsync(invoices, alpha, 100m, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 22));
        InvoiceDetailsDto overdue30 = await SentInvoiceAsync(invoices, alpha, 50m, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 23));
        await SentInvoiceAsync(invoices, beta, 25m, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 22));
        await SentInvoiceAsync(invoices, beta, 10m, new DateOnly(2026, 2, 1), new DateOnly(2026, 5, 24));
        await SentInvoiceAsync(invoices, alpha, 5m, new DateOnly(2026, 2, 1), new DateOnly(2026, 5, 23));
        InvoiceDetailsDto paid = await SentInvoiceAsync(invoices, beta, 80m, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 31));
        InvoiceResult january = await invoices.RecordPaymentAsync(Payment(paid, 80m, new DateOnly(2026, 1, 15)));
        Assert.True(january.Succeeded, string.Join("; ", january.Errors));

        InvoiceResult august = await invoices.RecordPaymentAsync(Payment(current, 40m, new DateOnly(2026, 8, 5)));
        Assert.True(august.Succeeded, string.Join("; ", august.Errors));

        InvoiceDetailsDto priorYear = await SentInvoiceAsync(invoices, alpha, 30m, new DateOnly(2025, 11, 1), new DateOnly(2025, 12, 15));
        InvoiceResult priorPay = await invoices.RecordPaymentAsync(Payment(priorYear, 30m, new DateOnly(2025, 12, 1)));
        Assert.True(priorPay.Succeeded, string.Join("; ", priorPay.Errors));

        ReportingResult<DashboardMetrics> dashboard = await reporting.GetDashboardAsync();
        Assert.True(dashboard.Succeeded, string.Join("; ", dashboard.Errors));
        DashboardMetrics metrics = dashboard.Value!;
        Assert.Equal(before.OutstandingReceivables + 150m, metrics.OutstandingReceivables);
        Assert.Equal(before.OverdueReceivables + 90m, metrics.OverdueReceivables);
        Assert.Equal(before.OpenInvoiceCount + 5, metrics.OpenInvoiceCount);
        Assert.Equal(before.OverdueInvoiceCount + 4, metrics.OverdueInvoiceCount);
        Assert.Equal(before.PaymentsThisMonth + 40m, metrics.PaymentsThisMonth);
        Assert.Equal(before.PaymentsThisYear + 120m, metrics.PaymentsThisYear);

        Assert.Equal(
            before.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Current).BalanceDue + 60m,
            metrics.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Current).BalanceDue);
        Assert.Equal(
            before.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Days1To30).BalanceDue + 50m,
            metrics.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Days1To30).BalanceDue);
        Assert.Equal(
            before.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Days31To60).BalanceDue + 25m,
            metrics.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Days31To60).BalanceDue);
        Assert.Equal(
            before.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Days61To90).BalanceDue + 10m,
            metrics.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Days61To90).BalanceDue);
        Assert.Equal(
            before.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Days90Plus).BalanceDue + 5m,
            metrics.Aging.Single(row => row.Bucket == InvoiceAgingBucket.Days90Plus).BalanceDue);

        await using var db = CreateDb();
        string sql = ReportingQueries.OpenReceivables(db.Invoices.AsNoTracking())
            .Select(invoice => new { invoice.BalanceDue, Overdue = invoice.DueDate < clock.Today })
            .GroupBy(row => row.Overdue)
            .Select(group => new { group.Key, Amount = group.Sum(row => row.BalanceDue), Count = group.Count() })
            .ToQueryString();
        Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BalanceDue", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvoiceLines", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Payment_reports_honor_date_range_and_net_reversals()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        IReportingService reporting = provider.GetRequiredService<IReportingService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        Guid clientId = await CreateClientAsync(provider, "Gamma");
        InvoiceDetailsDto sent = await SentInvoiceAsync(invoices, clientId, 100m, new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1));
        InvoiceResult receipt = await invoices.RecordPaymentAsync(Payment(sent, 70m, new DateOnly(2026, 3, 10)));
        Assert.True(receipt.Succeeded, string.Join("; ", receipt.Errors));
        InvoiceResult reversal = await invoices.ReversePaymentAsync(new ReversePaymentCommand
        {
            Id = receipt.Invoice!.Id,
            RowVersion = receipt.Invoice.RowVersion,
            PaymentId = receipt.Invoice.Payments[0].Id,
            Reason = "Entered on the wrong invoice"
        });
        Assert.True(reversal.Succeeded, string.Join("; ", reversal.Errors));
        InvoiceResult later = await invoices.RecordPaymentAsync(Payment(reversal.Invoice!, 70m, new DateOnly(2026, 4, 2)));
        Assert.True(later.Succeeded, string.Join("; ", later.Errors));

        ReportingResult<IReadOnlyList<MonthlyPaymentRow>> months = await reporting.GetPaymentsByMonthAsync(new ReportFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 4, 30),
            ClientId = clientId
        });
        Assert.True(months.Succeeded);
        Assert.Equal(70m, months.Value!.Single(row => row.Year == 2026 && row.Month == 3).Amount);
        Assert.Equal(70m, months.Value!.Single(row => row.Year == 2026 && row.Month == 4).Amount);

        ReportingResult<IReadOnlyList<MonthlyPaymentRow>> aprilBoundary = await reporting.GetPaymentsByMonthAsync(new ReportFilter
        {
            From = new DateOnly(2026, 4, 2),
            To = new DateOnly(2026, 4, 2),
            ClientId = clientId
        });
        Assert.True(aprilBoundary.Succeeded);
        Assert.Equal(70m, aprilBoundary.Value!.Single(row => row.Month == 4).Amount);

        ReportingResult<IReadOnlyList<MonthlyPaymentRow>> afterApril = await reporting.GetPaymentsByMonthAsync(new ReportFilter
        {
            From = new DateOnly(2026, 4, 3),
            To = new DateOnly(2026, 4, 3),
            ClientId = clientId
        });
        Assert.True(afterApril.Succeeded);
        Assert.Equal(0m, afterApril.Value!.Single().Amount);

        ReportingResult<IReadOnlyList<ClientRevenueRow>> clients = await reporting.GetRevenueByClientAsync(new ReportFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 3, 31),
            ClientId = clientId
        });
        Assert.True(clients.Succeeded);
        Assert.Equal(70m, clients.Value!.Single().Amount);

        ReportingResult<IReadOnlyList<PaymentHistoryRow>> history = await reporting.GetPaymentHistoryAsync(new ReportFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 4, 30),
            ClientId = clientId
        });
        Assert.True(history.Succeeded);
        Assert.Equal(2, history.Value!.Count);
        Assert.DoesNotContain(history.Value, row => row.IsReversal);

        ReportingResult<IReadOnlyList<PaymentHistoryRow>> reversalMonth = await reporting.GetPaymentHistoryAsync(new ReportFilter
        {
            From = new DateOnly(2026, 8, 1),
            To = new DateOnly(2026, 8, 22),
            ClientId = clientId
        });
        Assert.True(reversalMonth.Succeeded);
        Assert.Contains(reversalMonth.Value!, row => row.IsReversal && row.Amount == -70m);
    }

    [Fact]
    public async Task Outstanding_report_excludes_draft_void_and_paid_invoices()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        IReportingService reporting = provider.GetRequiredService<IReportingService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        Guid clientId = await CreateClientAsync(provider, "Delta");

        InvoiceResult draft = await invoices.CreateAsync(Header(clientId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10)));
        Assert.True(draft.Succeeded);
        await invoices.AddLineAsync(Line(draft.Invoice!, 99m));

        InvoiceDetailsDto voidable = await SentInvoiceAsync(invoices, clientId, 20m, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10));
        InvoiceResult voided = await invoices.VoidAsync(new VoidInvoiceCommand
        {
            Id = voidable.Id,
            RowVersion = voidable.RowVersion,
            Reason = "Cancelled work"
        });
        Assert.True(voided.Succeeded, string.Join("; ", voided.Errors));

        InvoiceDetailsDto open = await SentInvoiceAsync(invoices, clientId, 45m, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 20));
        ReportingResult<IReadOnlyList<OutstandingInvoiceRow>> result = await reporting.GetOutstandingInvoicesAsync(new ReportFilter
        {
            ClientId = clientId
        });
        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!, row => row.Id == open.Id && row.DaysOverdue == 2);
        Assert.DoesNotContain(result.Value!, row => row.Id == draft.Invoice!.Id);
        Assert.DoesNotContain(result.Value!, row => row.Id == voidable.Id);

        ReportingResult<IReadOnlyList<OutstandingInvoiceRow>> dueFiltered = await reporting.GetOutstandingInvoicesAsync(new ReportFilter
        {
            ClientId = clientId,
            From = new DateOnly(2026, 8, 21),
            To = new DateOnly(2026, 8, 31)
        });
        Assert.True(dueFiltered.Succeeded);
        Assert.DoesNotContain(dueFiltered.Value!, row => row.Id == open.Id);
    }

    [Fact]
    public async Task Csv_export_uses_safe_file_name_and_sanitized_cells()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        IReportingService reporting = provider.GetRequiredService<IReportingService>();
        Guid clientId = await CreateClientAsync(provider, $"={_marker}");
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        await SentInvoiceAsync(invoices, clientId, 12m, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10));

        ReportingResult<CsvExport> csv = await reporting.ExportOutstandingInvoicesCsvAsync(new ReportFilter
        {
            ClientId = clientId
        });
        Assert.True(csv.Succeeded);
        Assert.Equal(csv.Value!.FileName, Path.GetFileName(csv.Value.FileName));
        Assert.StartsWith("billfoundry-outstanding-invoices-", csv.Value.FileName, StringComparison.Ordinal);
        Assert.EndsWith(".csv", csv.Value.FileName, StringComparison.Ordinal);
        string text = Encoding.UTF8.GetString(csv.Value.Content);
        Assert.Contains("InvoiceNumber", text, StringComparison.Ordinal);
        Assert.Contains("'=" + _marker, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthenticated_caller_is_forbidden()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        IReportingService reporting = provider.GetRequiredService<IReportingService>();
        ReportingResult<DashboardMetrics> result = await reporting.GetDashboardAsync();
        Assert.True(result.IsForbidden);
        Assert.False(result.Succeeded);
    }

    private async Task<Guid> CreateClientAsync(IServiceProvider provider, string name)
    {
        IClientService clients = provider.GetRequiredService<IClientService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand
        {
            Name = name.StartsWith('=') ? name : $"{_marker} {name}"
        });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        return client.Client!.Id;
    }

    private async Task<InvoiceDetailsDto> SentInvoiceAsync(
        IInvoiceService invoices,
        Guid clientId,
        decimal amount,
        DateOnly issueDate,
        DateOnly dueDate)
    {
        InvoiceResult created = await invoices.CreateAsync(Header(clientId, issueDate, dueDate));
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, amount));
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));
        InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = lined.Invoice!.Id,
            RowVersion = lined.Invoice.RowVersion
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));
        return sent.Invoice!;
    }

    private SaveInvoiceCommand Header(Guid clientId, DateOnly issueDate, DateOnly dueDate) =>
        new()
        {
            ClientId = clientId,
            IssueDate = issueDate,
            DueDate = dueDate,
            Notes = _marker
        };

    private static SaveInvoiceLineCommand Line(InvoiceDetailsDto invoice, decimal amount) =>
        new()
        {
            Id = invoice.Id,
            RowVersion = invoice.RowVersion,
            Description = "Work",
            Quantity = 1m,
            Unit = CatalogUnitType.Hour,
            UnitPrice = amount,
            IsTaxable = false
        };

    private static RecordPaymentCommand Payment(InvoiceDetailsDto invoice, decimal amount, DateOnly paymentDate) =>
        new()
        {
            Id = invoice.Id,
            RowVersion = invoice.RowVersion,
            PaymentDate = paymentDate,
            Amount = amount,
            Method = PaymentMethod.BankTransfer
        };

    private BillFoundryDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(_sql.ConnectionString)
            .Options);

    private sealed class FixedDateTimeProvider(DateOnly today) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        public DateOnly Today => today;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}

using System.Globalization;
using BillFoundry.Application.Reporting;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Invoices;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Reporting;

internal sealed class ReportingService(
    BillFoundryDbContext dbContext,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IReportingService
{
    public const int MaxListRows = 10_000;

    public Task<ReportingResult<DashboardMetrics>> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(async () =>
        {
            DateOnly today = Today();
            string currency = await CurrencyCodeAsync(cancellationToken).ConfigureAwait(false);
            ReceivableTotals receivables = await SumReceivablesAsync(today, cancellationToken).ConfigureAwait(false);
            (decimal month, decimal year) = await SumPaymentsAsync(today, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<AgingBucketRow> aging = await LoadAgingAsync(today, cancellationToken).ConfigureAwait(false);
            DateOnly yearStart = new(today.Year, 1, 1);
            IReadOnlyList<MonthlyPaymentRow> months = await LoadPaymentsByMonthAsync(yearStart, today, null, cancellationToken)
                .ConfigureAwait(false);

            return new DashboardMetrics
            {
                AsOf = today,
                CurrencyCode = currency,
                OutstandingReceivables = receivables.Outstanding,
                OverdueReceivables = receivables.Overdue,
                PaymentsThisMonth = month,
                PaymentsThisYear = year,
                OpenInvoiceCount = receivables.OpenCount,
                OverdueInvoiceCount = receivables.OverdueCount,
                Aging = aging,
                PaymentsByMonth = months
            };
        }, cancellationToken);

    public Task<ReportingResult<AgingReport>> GetAgingAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        LoadAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(filter);
            filter.Normalize();
            DateOnly asOf = filter.AsOf ?? Today();
            string currency = await CurrencyCodeAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<AgingBucketRow> buckets = await LoadAgingAsync(asOf, cancellationToken).ConfigureAwait(false);
            return new AgingReport
            {
                AsOf = asOf,
                CurrencyCode = currency,
                Buckets = buckets
            };
        }, cancellationToken);

    public Task<ReportingResult<IReadOnlyList<MonthlyPaymentRow>>> GetPaymentsByMonthAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        LoadAsync(async () =>
        {
            (DateOnly from, DateOnly to) = PaymentRange(filter);
            return await LoadPaymentsByMonthAsync(from, to, filter.ClientId, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task<ReportingResult<IReadOnlyList<ClientRevenueRow>>> GetRevenueByClientAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        LoadAsync(async () =>
        {
            (DateOnly from, DateOnly to) = PaymentRange(filter);
            ArgumentNullException.ThrowIfNull(filter);
            IQueryable<InvoicePayment> payments = PaymentRows(from, to);
            IQueryable<Invoice> invoices = dbContext.Invoices.AsNoTracking();
            if (filter.ClientId is Guid clientId)
            {
                invoices = invoices.Where(invoice => invoice.ClientId == clientId);
            }

            List<ClientRevenueRow> rows = await (
                    from payment in payments
                    join invoice in invoices on payment.InvoiceId equals invoice.Id
                    group new { payment, invoice } by new
                    {
                        invoice.ClientId,
                        invoice.ClientSnapshot.Name,
                        invoice.ClientSnapshot.Code
                    }
                    into grouped
                    select new ClientRevenueRow
                    {
                        ClientId = grouped.Key.ClientId,
                        ClientName = grouped.Key.Name,
                        ClientCode = grouped.Key.Code,
                        Amount = grouped.Sum(row =>
                            row.payment.ReversesPaymentId == null ? row.payment.Amount : -row.payment.Amount),
                        PaymentCount = grouped.Count(row => row.payment.ReversesPaymentId == null)
                    })
                .OrderByDescending(row => row.Amount)
                .ThenBy(row => row.ClientName)
                .Take(MaxListRows)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<ClientRevenueRow>)rows;
        }, cancellationToken);

    public Task<ReportingResult<IReadOnlyList<OutstandingInvoiceRow>>> GetOutstandingInvoicesAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        LoadAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(filter);
            filter.Normalize();
            DateOnly asOf = filter.AsOf ?? Today();
            IQueryable<Invoice> invoices = ReportingQueries.OpenReceivables(dbContext.Invoices.AsNoTracking());
            if (filter.ClientId is Guid clientId)
            {
                invoices = invoices.Where(invoice => invoice.ClientId == clientId);
            }

            if (filter.From is DateOnly from)
            {
                invoices = invoices.Where(invoice => invoice.DueDate >= from);
            }

            if (filter.To is DateOnly to)
            {
                invoices = invoices.Where(invoice => invoice.DueDate <= to);
            }

            var projected = await invoices
                .OrderBy(invoice => invoice.DueDate)
                .ThenBy(invoice => invoice.Number)
                .Take(MaxListRows)
                .Select(invoice => new
                {
                    invoice.Id,
                    invoice.Number,
                    invoice.ClientId,
                    ClientName = invoice.ClientSnapshot.Name,
                    invoice.IssueDate,
                    invoice.DueDate,
                    invoice.Status,
                    invoice.Total,
                    invoice.AmountPaid,
                    invoice.BalanceDue,
                    CurrencyCode = invoice.Currency.Value
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<OutstandingInvoiceRow>)projected.Select(invoice =>
            {
                InvoiceStatus effective = invoice.DueDate < asOf ? InvoiceStatus.Overdue : invoice.Status;
                return new OutstandingInvoiceRow
                {
                    Id = invoice.Id,
                    Number = invoice.Number,
                    ClientId = invoice.ClientId,
                    ClientName = invoice.ClientName,
                    IssueDate = invoice.IssueDate,
                    DueDate = invoice.DueDate,
                    EffectiveStatus = effective,
                    StatusLabel = InvoiceStatusRules.Label(effective),
                    Total = invoice.Total,
                    AmountPaid = invoice.AmountPaid,
                    BalanceDue = invoice.BalanceDue,
                    DaysOverdue = InvoiceAging.DaysOverdue(invoice.DueDate, asOf),
                    CurrencyCode = invoice.CurrencyCode
                };
            }).ToList();
        }, cancellationToken);

    public Task<ReportingResult<IReadOnlyList<PaymentHistoryRow>>> GetPaymentHistoryAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        LoadAsync(async () =>
        {
            (DateOnly from, DateOnly to) = PaymentRange(filter);
            ArgumentNullException.ThrowIfNull(filter);
            IQueryable<InvoicePayment> payments = PaymentRows(from, to);
            IQueryable<Invoice> invoices = dbContext.Invoices.AsNoTracking();
            if (filter.ClientId is Guid clientId)
            {
                invoices = invoices.Where(invoice => invoice.ClientId == clientId);
            }

            List<PaymentHistoryRow> rows = await (
                    from payment in payments
                    join invoice in invoices on payment.InvoiceId equals invoice.Id
                    orderby payment.PaymentDate descending, invoice.Number
                    select new PaymentHistoryRow
                    {
                        PaymentId = payment.Id,
                        PaymentDate = payment.PaymentDate,
                        Amount = payment.ReversesPaymentId == null ? payment.Amount : -payment.Amount,
                        IsReversal = payment.ReversesPaymentId != null,
                        Method = payment.Method,
                        MethodLabel = "",
                        Reference = payment.Reference,
                        InvoiceId = invoice.Id,
                        InvoiceNumber = invoice.Number,
                        ClientId = invoice.ClientId,
                        ClientName = invoice.ClientSnapshot.Name,
                        CurrencyCode = invoice.Currency.Value
                    })
                .Take(MaxListRows)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<PaymentHistoryRow>)rows
                .Select(row => row with { MethodLabel = PaymentMethodDisplay.Label(row.Method) })
                .ToList();
        }, cancellationToken);

    public Task<ReportingResult<CsvExport>> ExportAgingCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        ExportAsync(GetAgingAsync(filter, cancellationToken), report =>
        {
            string[] headers = ["Bucket", "InvoiceCount", "BalanceDue", "AsOf", "Currency"];
            IEnumerable<string[]> rows = report.Buckets.Select(bucket => new[]
            {
                bucket.Label,
                CsvFormatter.Integer(bucket.InvoiceCount),
                CsvFormatter.Money(bucket.BalanceDue),
                CsvFormatter.Date(report.AsOf),
                report.CurrencyCode
            });
            return ToCsv("aging", report.AsOf, headers, rows);
        });

    public Task<ReportingResult<CsvExport>> ExportPaymentsByMonthCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        ExportAsync(GetPaymentsByMonthAsync(filter, cancellationToken), rows =>
        {
            string[] headers = ["Period", "Year", "Month", "Amount", "ReceiptCount"];
            DateOnly asOf = filter.To ?? Today();
            IEnumerable<string[]> csvRows = rows.Select(row => new[]
            {
                row.PeriodLabel,
                CsvFormatter.Integer(row.Year),
                CsvFormatter.Integer(row.Month),
                CsvFormatter.Money(row.Amount),
                CsvFormatter.Integer(row.PaymentCount)
            });
            return ToCsv("payments-by-month", asOf, headers, csvRows);
        });

    public Task<ReportingResult<CsvExport>> ExportRevenueByClientCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        ExportAsync(GetRevenueByClientAsync(filter, cancellationToken), rows =>
        {
            string[] headers = ["ClientName", "ClientCode", "Amount", "ReceiptCount"];
            DateOnly asOf = filter.To ?? Today();
            IEnumerable<string[]> csvRows = rows.Select(row => new[]
            {
                row.ClientName,
                row.ClientCode,
                CsvFormatter.Money(row.Amount),
                CsvFormatter.Integer(row.PaymentCount)
            });
            return ToCsv("revenue-by-client", asOf, headers, csvRows);
        });

    public Task<ReportingResult<CsvExport>> ExportOutstandingInvoicesCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        ExportAsync(GetOutstandingInvoicesAsync(filter, cancellationToken), rows =>
        {
            string[] headers =
            [
                "InvoiceNumber", "ClientName", "IssueDate", "DueDate", "Status", "Total", "AmountPaid",
                "BalanceDue", "DaysOverdue", "Currency"
            ];
            DateOnly asOf = filter.AsOf ?? Today();
            IEnumerable<string[]> csvRows = rows.Select(row => new[]
            {
                row.Number,
                row.ClientName,
                CsvFormatter.Date(row.IssueDate),
                CsvFormatter.Date(row.DueDate),
                row.StatusLabel,
                CsvFormatter.Money(row.Total),
                CsvFormatter.Money(row.AmountPaid),
                CsvFormatter.Money(row.BalanceDue),
                CsvFormatter.Integer(row.DaysOverdue),
                row.CurrencyCode
            });
            return ToCsv("outstanding-invoices", asOf, headers, csvRows);
        });

    public Task<ReportingResult<CsvExport>> ExportPaymentHistoryCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default) =>
        ExportAsync(GetPaymentHistoryAsync(filter, cancellationToken), rows =>
        {
            string[] headers =
            [
                "PaymentDate", "InvoiceNumber", "ClientName", "Amount", "IsReversal", "Method", "Reference",
                "Currency"
            ];
            DateOnly asOf = filter.To ?? Today();
            IEnumerable<string[]> csvRows = rows.Select(row => new[]
            {
                CsvFormatter.Date(row.PaymentDate),
                row.InvoiceNumber,
                row.ClientName,
                CsvFormatter.Money(row.Amount),
                CsvFormatter.Boolean(row.IsReversal),
                row.MethodLabel,
                row.Reference ?? string.Empty,
                row.CurrencyCode
            });
            return ToCsv("payment-history", asOf, headers, csvRows);
        });

    private async Task<ReportingResult<T>> LoadAsync<T>(Func<Task<T>> load, CancellationToken cancellationToken)
    {
        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ReportingResult<T>.Forbidden();
        }

        T value = await load().ConfigureAwait(false);
        return ReportingResult<T>.Success(value);
    }

    private async Task<ReportingResult<CsvExport>> ExportAsync<T>(
        Task<ReportingResult<T>> source,
        Func<T, CsvExport> map)
    {
        ReportingResult<T> result = await source.ConfigureAwait(false);
        if (result.IsForbidden)
        {
            return ReportingResult<CsvExport>.Forbidden();
        }

        if (!result.Succeeded || result.Value is null)
        {
            return ReportingResult<CsvExport>.Invalid(result.Errors);
        }

        return ReportingResult<CsvExport>.Success(map(result.Value));
    }

    private async Task<ReceivableTotals> SumReceivablesAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var rows = await ReportingQueries.OpenReceivables(dbContext.Invoices.AsNoTracking())
            .Select(invoice => new { invoice.BalanceDue, Overdue = invoice.DueDate < asOf })
            .GroupBy(row => row.Overdue)
            .Select(group => new
            {
                Overdue = group.Key,
                Amount = group.Sum(row => row.BalanceDue),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal outstanding = rows.Sum(row => row.Amount);
        int openCount = rows.Sum(row => row.Count);
        var overdue = rows.FirstOrDefault(row => row.Overdue);
        return new ReceivableTotals(outstanding, overdue?.Amount ?? 0m, openCount, overdue?.Count ?? 0);
    }

    private async Task<(decimal Month, decimal Year)> SumPaymentsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        DateOnly monthStart = new(today.Year, today.Month, 1);
        DateOnly yearStart = new(today.Year, 1, 1);

        var rows = await dbContext.InvoicePayments.AsNoTracking()
            .Where(payment => payment.PaymentDate >= yearStart && payment.PaymentDate <= today)
            .Select(payment => new
            {
                InMonth = payment.PaymentDate >= monthStart,
                Amount = payment.ReversesPaymentId == null ? payment.Amount : -payment.Amount
            })
            .GroupBy(row => row.InMonth)
            .Select(group => new { InMonth = group.Key, Amount = group.Sum(row => row.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal month = rows.FirstOrDefault(row => row.InMonth)?.Amount ?? 0m;
        decimal year = rows.Sum(row => row.Amount);
        return (month, year);
    }

    private async Task<IReadOnlyList<AgingBucketRow>> LoadAgingAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var rows = await ReportingQueries.OpenReceivables(dbContext.Invoices.AsNoTracking())
            .Select(invoice => new
            {
                invoice.BalanceDue,
                Bucket = invoice.DueDate >= asOf
                    ? 0
                    : EF.Functions.DateDiffDay(invoice.DueDate, asOf) <= 30
                        ? 1
                        : EF.Functions.DateDiffDay(invoice.DueDate, asOf) <= 60
                            ? 2
                            : EF.Functions.DateDiffDay(invoice.DueDate, asOf) <= 90
                                ? 3
                                : 4
            })
            .GroupBy(row => row.Bucket)
            .Select(group => new
            {
                Bucket = group.Key,
                Count = group.Count(),
                Amount = group.Sum(row => row.BalanceDue)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<int, (int Count, decimal Amount)> map = rows.ToDictionary(
            row => row.Bucket,
            row => (row.Count, row.Amount));

        return InvoiceAging.All.Select(bucket =>
        {
            map.TryGetValue((int)bucket, out (int Count, decimal Amount) found);
            return new AgingBucketRow
            {
                Bucket = bucket,
                Label = InvoiceAging.Label(bucket),
                InvoiceCount = found.Count,
                BalanceDue = found.Amount
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<MonthlyPaymentRow>> LoadPaymentsByMonthAsync(
        DateOnly from,
        DateOnly to,
        Guid? clientId,
        CancellationToken cancellationToken)
    {
        var rows = await PaymentRows(from, to, clientId)
            .GroupBy(payment => new { payment.PaymentDate.Year, payment.PaymentDate.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Amount = group.Sum(payment =>
                    payment.ReversesPaymentId == null ? payment.Amount : -payment.Amount),
                PaymentCount = group.Count(payment => payment.ReversesPaymentId == null)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<(int Year, int Month), (decimal Amount, int Count)> map = rows.ToDictionary(
            row => (row.Year, row.Month),
            row => (row.Amount, row.PaymentCount));

        var filled = new List<MonthlyPaymentRow>();
        DateOnly cursor = new(from.Year, from.Month, 1);
        DateOnly last = new(to.Year, to.Month, 1);
        while (cursor <= last)
        {
            map.TryGetValue((cursor.Year, cursor.Month), out (decimal Amount, int Count) found);
            filled.Add(new MonthlyPaymentRow
            {
                Year = cursor.Year,
                Month = cursor.Month,
                PeriodLabel = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Amount = found.Amount,
                PaymentCount = found.Count
            });
            cursor = cursor.AddMonths(1);
        }

        return filled;
    }

    private IQueryable<InvoicePayment> PaymentRows(DateOnly from, DateOnly to, Guid? clientId = null)
    {
        IQueryable<InvoicePayment> payments = ReportingQueries.PaymentsThrough(
            ReportingQueries.PaymentsOnOrAfter(dbContext.InvoicePayments.AsNoTracking(), from),
            to);
        if (clientId is Guid id)
        {
            payments = payments.Where(payment =>
                dbContext.Invoices.Any(invoice => invoice.Id == payment.InvoiceId && invoice.ClientId == id));
        }

        return payments;
    }

    private (DateOnly From, DateOnly To) PaymentRange(ReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Normalize();
        DateOnly today = Today();
        DateOnly from = filter.From ?? new DateOnly(today.Year, 1, 1);
        DateOnly to = filter.To ?? today;
        if (to < from)
        {
            to = from;
        }

        return (from, to);
    }

    private async Task<string> CurrencyCodeAsync(CancellationToken cancellationToken)
    {
        string? code = await dbContext.Organizations.AsNoTracking()
            .Select(organization => organization.DefaultCurrency.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(code) ? CurrencyCode.Usd.Value : code;
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private async Task<bool> IsForbiddenAsync()
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.ManageInvoices)
            .ConfigureAwait(false);
        return !authorization.Succeeded;
    }

    private static CsvExport ToCsv(
        string reportKey,
        DateOnly asOf,
        IReadOnlyList<string> headers,
        IEnumerable<string[]> rows) =>
        new()
        {
            FileName = CsvFormatter.FileName(reportKey, asOf),
            ContentType = CsvFormatter.ContentType,
            Content = CsvFormatter.ToUtf8(headers, rows)
        };

    private readonly record struct ReceivableTotals(
        decimal Outstanding,
        decimal Overdue,
        int OpenCount,
        int OverdueCount);
}

using BillFoundry.Domain.Invoices;

namespace BillFoundry.Application.Reporting;

public sealed class ReportFilter
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public Guid? ClientId { get; set; }

    public DateOnly? AsOf { get; set; }

    public void Normalize()
    {
        if (ClientId == Guid.Empty)
        {
            ClientId = null;
        }

        if (From is DateOnly from && from == default)
        {
            From = null;
        }

        if (To is DateOnly to && to == default)
        {
            To = null;
        }

        if (AsOf is DateOnly asOf && asOf == default)
        {
            AsOf = null;
        }

        if (From is DateOnly start && To is DateOnly end && end < start)
        {
            To = start;
        }
    }
}

public sealed class DashboardMetrics
{
    public required DateOnly AsOf { get; init; }

    public required string CurrencyCode { get; init; }

    public required decimal OutstandingReceivables { get; init; }

    public required decimal OverdueReceivables { get; init; }

    public required decimal PaymentsThisMonth { get; init; }

    public required decimal PaymentsThisYear { get; init; }

    public required int OpenInvoiceCount { get; init; }

    public required int OverdueInvoiceCount { get; init; }

    public required IReadOnlyList<AgingBucketRow> Aging { get; init; }

    public required IReadOnlyList<MonthlyPaymentRow> PaymentsByMonth { get; init; }
}

public sealed class AgingReport
{
    public required DateOnly AsOf { get; init; }

    public required string CurrencyCode { get; init; }

    public required IReadOnlyList<AgingBucketRow> Buckets { get; init; }

    public decimal TotalBalance => Buckets.Sum(bucket => bucket.BalanceDue);

    public int TotalCount => Buckets.Sum(bucket => bucket.InvoiceCount);
}

public sealed class AgingBucketRow
{
    public required InvoiceAgingBucket Bucket { get; init; }

    public required string Label { get; init; }

    public required int InvoiceCount { get; init; }

    public required decimal BalanceDue { get; init; }
}

public sealed class MonthlyPaymentRow
{
    public required int Year { get; init; }

    public required int Month { get; init; }

    public required string PeriodLabel { get; init; }

    public required decimal Amount { get; init; }

    public required int PaymentCount { get; init; }
}

public sealed class ClientRevenueRow
{
    public required Guid ClientId { get; init; }

    public required string ClientName { get; init; }

    public required string ClientCode { get; init; }

    public required decimal Amount { get; init; }

    public required int PaymentCount { get; init; }
}

public sealed class OutstandingInvoiceRow
{
    public required Guid Id { get; init; }

    public required string Number { get; init; }

    public required Guid ClientId { get; init; }

    public required string ClientName { get; init; }

    public required DateOnly IssueDate { get; init; }

    public required DateOnly DueDate { get; init; }

    public required InvoiceStatus EffectiveStatus { get; init; }

    public required string StatusLabel { get; init; }

    public required decimal Total { get; init; }

    public required decimal AmountPaid { get; init; }

    public required decimal BalanceDue { get; init; }

    public required int DaysOverdue { get; init; }

    public required string CurrencyCode { get; init; }
}

public sealed record PaymentHistoryRow
{
    public required Guid PaymentId { get; init; }

    public required DateOnly PaymentDate { get; init; }

    public required decimal Amount { get; init; }

    public required bool IsReversal { get; init; }

    public required PaymentMethod Method { get; init; }

    public required string MethodLabel { get; init; }

    public string? Reference { get; init; }

    public required Guid InvoiceId { get; init; }

    public required string InvoiceNumber { get; init; }

    public required Guid ClientId { get; init; }

    public required string ClientName { get; init; }

    public required string CurrencyCode { get; init; }
}

public sealed class CsvExport
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required byte[] Content { get; init; }
}

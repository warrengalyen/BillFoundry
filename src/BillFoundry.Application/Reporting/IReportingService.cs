namespace BillFoundry.Application.Reporting;

/// <summary>
/// Operational receivables and payment reports. Reads require
/// <c>ManageInvoices</c>. Totals are aggregated in SQL from persisted invoice
/// balances and payment rows; catalog prices are not recomputed.
/// </summary>
public interface IReportingService
{
    Task<ReportingResult<DashboardMetrics>> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<ReportingResult<AgingReport>> GetAgingAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<IReadOnlyList<MonthlyPaymentRow>>> GetPaymentsByMonthAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<IReadOnlyList<ClientRevenueRow>>> GetRevenueByClientAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<IReadOnlyList<OutstandingInvoiceRow>>> GetOutstandingInvoicesAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<IReadOnlyList<PaymentHistoryRow>>> GetPaymentHistoryAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<CsvExport>> ExportAgingCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<CsvExport>> ExportPaymentsByMonthCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<CsvExport>> ExportRevenueByClientCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<CsvExport>> ExportOutstandingInvoicesCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReportingResult<CsvExport>> ExportPaymentHistoryCsvAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default);
}

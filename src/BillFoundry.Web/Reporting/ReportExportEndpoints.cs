using BillFoundry.Application.Reporting;
using BillFoundry.Application.Security;
using Microsoft.AspNetCore.Http;

namespace BillFoundry.Web.Reporting;

internal static class ReportExportEndpoints
{
    public static IEndpointRouteBuilder MapReportExports(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/Reports/aging.csv", ExportAgingAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageInvoices)
            .WithName("AgingCsv");

        endpoints.MapGet("/Reports/payments-by-month.csv", ExportPaymentsByMonthAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageInvoices)
            .WithName("PaymentsByMonthCsv");

        endpoints.MapGet("/Reports/revenue-by-client.csv", ExportRevenueByClientAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageInvoices)
            .WithName("RevenueByClientCsv");

        endpoints.MapGet("/Reports/outstanding.csv", ExportOutstandingAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageInvoices)
            .WithName("OutstandingCsv");

        endpoints.MapGet("/Reports/payment-history.csv", ExportPaymentHistoryAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageInvoices)
            .WithName("PaymentHistoryCsv");

        return endpoints;
    }

    private static Task<IResult> ExportAgingAsync(
        DateOnly? asOf,
        IReportingService reporting,
        CancellationToken cancellationToken) =>
        FileAsync(reporting.ExportAgingCsvAsync(ReportQuery.Create(null, null, null, asOf), cancellationToken));

    private static Task<IResult> ExportPaymentsByMonthAsync(
        DateOnly? from,
        DateOnly? to,
        IReportingService reporting,
        CancellationToken cancellationToken) =>
        FileAsync(reporting.ExportPaymentsByMonthCsvAsync(ReportQuery.Create(from, to, null, null), cancellationToken));

    private static Task<IResult> ExportRevenueByClientAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? clientId,
        IReportingService reporting,
        CancellationToken cancellationToken) =>
        FileAsync(reporting.ExportRevenueByClientCsvAsync(ReportQuery.Create(from, to, clientId, null), cancellationToken));

    private static Task<IResult> ExportOutstandingAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? clientId,
        DateOnly? asOf,
        IReportingService reporting,
        CancellationToken cancellationToken) =>
        FileAsync(reporting.ExportOutstandingInvoicesCsvAsync(ReportQuery.Create(from, to, clientId, asOf), cancellationToken));

    private static Task<IResult> ExportPaymentHistoryAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? clientId,
        IReportingService reporting,
        CancellationToken cancellationToken) =>
        FileAsync(reporting.ExportPaymentHistoryCsvAsync(ReportQuery.Create(from, to, clientId, null), cancellationToken));

    private static async Task<IResult> FileAsync(Task<ReportingResult<CsvExport>> source)
    {
        ReportingResult<CsvExport> result = await source.ConfigureAwait(false);
        if (result.IsForbidden)
        {
            return Results.Forbid();
        }

        if (!result.Succeeded || result.Value is null)
        {
            return Results.BadRequest();
        }

        return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}

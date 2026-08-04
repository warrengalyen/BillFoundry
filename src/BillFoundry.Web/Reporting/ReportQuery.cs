using BillFoundry.Application.Reporting;

namespace BillFoundry.Web.Reporting;

internal static class ReportQuery
{
    public static ReportFilter Create(DateOnly? from, DateOnly? to, Guid? clientId, DateOnly? asOf)
    {
        var filter = new ReportFilter
        {
            From = from,
            To = to,
            ClientId = clientId,
            AsOf = asOf
        };
        filter.Normalize();
        return filter;
    }

    public static string AgingCsv(DateOnly? asOf) =>
        Append("/Reports/aging.csv", ("asOf", Format(asOf)));

    public static string PaymentsByMonthCsv(DateOnly? from, DateOnly? to) =>
        Append("/Reports/payments-by-month.csv", ("from", Format(from)), ("to", Format(to)));

    public static string RevenueByClientCsv(DateOnly? from, DateOnly? to, Guid? clientId) =>
        Append(
            "/Reports/revenue-by-client.csv",
            ("from", Format(from)),
            ("to", Format(to)),
            ("clientId", clientId is Guid id && id != Guid.Empty ? id.ToString() : null));

    public static string OutstandingCsv(DateOnly? from, DateOnly? to, Guid? clientId, DateOnly? asOf) =>
        Append(
            "/Reports/outstanding.csv",
            ("from", Format(from)),
            ("to", Format(to)),
            ("clientId", clientId is Guid id && id != Guid.Empty ? id.ToString() : null),
            ("asOf", Format(asOf)));

    public static string PaymentHistoryCsv(DateOnly? from, DateOnly? to, Guid? clientId) =>
        Append(
            "/Reports/payment-history.csv",
            ("from", Format(from)),
            ("to", Format(to)),
            ("clientId", clientId is Guid id && id != Guid.Empty ? id.ToString() : null));

    private static string? Format(DateOnly? value) => value?.ToString("yyyy-MM-dd");

    private static string Append(string path, params (string Key, string? Value)[] pairs)
    {
        var parts = new List<string>();
        foreach ((string key, string? value) in pairs)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }
}

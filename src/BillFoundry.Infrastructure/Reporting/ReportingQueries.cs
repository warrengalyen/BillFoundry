using BillFoundry.Domain.Invoices;

namespace BillFoundry.Infrastructure.Reporting;

internal static class ReportingQueries
{
    public static IQueryable<Invoice> OpenReceivables(IQueryable<Invoice> invoices) =>
        invoices.Where(invoice =>
            (invoice.Status == InvoiceStatus.Sent || invoice.Status == InvoiceStatus.PartiallyPaid)
            && invoice.BalanceDue > 0m);

    public static IQueryable<Invoice> OverdueReceivables(IQueryable<Invoice> invoices, DateOnly asOf) =>
        OpenReceivables(invoices).Where(invoice => invoice.DueDate < asOf);

    public static IQueryable<InvoicePayment> PaymentsOnOrAfter(IQueryable<InvoicePayment> payments, DateOnly from) =>
        payments.Where(payment => payment.PaymentDate >= from);

    public static IQueryable<InvoicePayment> PaymentsThrough(IQueryable<InvoicePayment> payments, DateOnly to) =>
        payments.Where(payment => payment.PaymentDate <= to);
}

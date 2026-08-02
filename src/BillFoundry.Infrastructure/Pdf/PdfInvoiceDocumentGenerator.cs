using BillFoundry.Application.Documents;

namespace BillFoundry.Infrastructure.Pdf;

internal sealed class PdfInvoiceDocumentGenerator : IInvoiceDocumentGenerator
{
    public GeneratedDocument Generate(InvoiceDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        byte[] content = FinancialDocumentPdfWriter.Write(new FinancialPdfModel
        {
            Title = "Invoice",
            Number = model.Number,
            StatusLabel = model.StatusLabel,
            IssueDate = model.IssueDate,
            SecondaryDate = model.DueDate,
            SecondaryDateLabel = "Due date",
            ReferenceLabel = string.IsNullOrWhiteSpace(model.PurchaseOrder) ? null : "Purchase order",
            ReferenceValue = model.PurchaseOrder,
            Issuer = model.Issuer,
            Client = model.Client,
            CurrencyCode = model.CurrencyCode,
            Lines = model.Lines,
            Subtotal = model.Subtotal,
            Discount = model.Discount,
            TaxRatePercent = model.TaxRatePercent,
            Tax = model.Tax,
            Total = model.Total,
            AmountPaid = model.AmountPaid,
            BalanceDue = model.BalanceDue,
            Notes = model.Notes,
            ClosingTitle = "Payment instructions",
            ClosingText = model.PaymentInstructions,
            ShowVoidMark = model.IsVoid
        });

        return new GeneratedDocument
        {
            FileName = DocumentFileName.ForInvoice(model.Number),
            ContentType = "application/pdf",
            Content = content
        };
    }
}

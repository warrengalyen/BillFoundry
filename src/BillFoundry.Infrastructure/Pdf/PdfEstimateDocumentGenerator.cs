using BillFoundry.Application.Documents;

namespace BillFoundry.Infrastructure.Pdf;

internal sealed class PdfEstimateDocumentGenerator : IEstimateDocumentGenerator
{
    public GeneratedDocument Generate(EstimateDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        byte[] content = FinancialDocumentPdfWriter.Write(new FinancialPdfModel
        {
            Title = "Estimate",
            Number = model.Number,
            StatusLabel = model.StatusLabel,
            IssueDate = model.IssueDate,
            SecondaryDate = model.ExpirationDate,
            SecondaryDateLabel = model.ExpirationDate is null ? null : "Expiration",
            Issuer = model.Issuer,
            Client = model.Client,
            CurrencyCode = model.CurrencyCode,
            Lines = model.Lines,
            Subtotal = model.Subtotal,
            Discount = model.Discount,
            TaxRatePercent = model.TaxRatePercent,
            Tax = model.Tax,
            Total = model.Total,
            Notes = model.Notes,
            ClosingTitle = "Terms",
            ClosingText = model.Terms,
            ShowVoidMark = false
        });

        return new GeneratedDocument
        {
            FileName = DocumentFileName.ForEstimate(model.Number),
            ContentType = "application/pdf",
            Content = content
        };
    }
}

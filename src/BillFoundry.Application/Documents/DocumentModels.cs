namespace BillFoundry.Application.Documents;

public sealed class GeneratedDocument
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required byte[] Content { get; init; }
}

public sealed class DocumentResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public bool IsNotFound { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public GeneratedDocument? Document { get; private init; }

    public static DocumentResult Success(GeneratedDocument document) =>
        new()
        {
            Succeeded = true,
            Document = document
        };

    public static DocumentResult Forbidden(string message) =>
        new()
        {
            IsForbidden = true,
            Errors = [message]
        };

    public static DocumentResult NotFound(string message) =>
        new()
        {
            IsNotFound = true,
            Errors = [message]
        };

    public static DocumentResult Invalid(IReadOnlyList<string> errors) =>
        new()
        {
            Errors = errors
        };
}

public sealed record DocumentPartyModel
{
    public required string Name { get; init; }

    public string? Code { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public IReadOnlyList<string> AddressLines { get; init; } = [];
}

public sealed record DocumentIssuerModel
{
    public required string LegalName { get; init; }

    public required string DisplayName { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Website { get; init; }

    public string? TaxId { get; init; }

    public IReadOnlyList<string> AddressLines { get; init; } = [];

    public byte[]? LogoBytes { get; init; }
}

public sealed record DocumentLineModel
{
    public required string Description { get; init; }

    public required decimal Quantity { get; init; }

    public required string UnitLabel { get; init; }

    public required decimal UnitPrice { get; init; }

    public required decimal LineAmount { get; init; }
}

public sealed record InvoiceDocumentModel
{
    public required DocumentIssuerModel Issuer { get; init; }

    public required DocumentPartyModel Client { get; init; }

    public required string Number { get; init; }

    public required string StatusLabel { get; init; }

    public required DateOnly IssueDate { get; init; }

    public required DateOnly DueDate { get; init; }

    public string? PurchaseOrder { get; init; }

    public required string CurrencyCode { get; init; }

    public required IReadOnlyList<DocumentLineModel> Lines { get; init; }

    public required decimal Subtotal { get; init; }

    public required decimal Discount { get; init; }

    public required decimal TaxRatePercent { get; init; }

    public required decimal Tax { get; init; }

    public required decimal Total { get; init; }

    public required decimal AmountPaid { get; init; }

    public required decimal BalanceDue { get; init; }

    public string? Notes { get; init; }

    public string? PaymentInstructions { get; init; }

    public required bool IsVoid { get; init; }
}

public sealed record EstimateDocumentModel
{
    public required DocumentIssuerModel Issuer { get; init; }

    public required DocumentPartyModel Client { get; init; }

    public required string Number { get; init; }

    public required string StatusLabel { get; init; }

    public required DateOnly IssueDate { get; init; }

    public DateOnly? ExpirationDate { get; init; }

    public required string CurrencyCode { get; init; }

    public required IReadOnlyList<DocumentLineModel> Lines { get; init; }

    public required decimal Subtotal { get; init; }

    public required decimal Discount { get; init; }

    public required decimal TaxRatePercent { get; init; }

    public required decimal Tax { get; init; }

    public required decimal Total { get; init; }

    public string? Notes { get; init; }

    public string? Terms { get; init; }
}

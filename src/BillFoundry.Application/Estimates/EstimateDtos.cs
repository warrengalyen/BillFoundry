using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;

namespace BillFoundry.Application.Estimates;

public sealed class EstimateListItemDto
{
    public required Guid Id { get; init; }

    public required string Number { get; init; }

    public required Guid ClientId { get; init; }

    public required string ClientName { get; init; }

    public required DateOnly IssueDate { get; init; }

    public DateOnly? ExpirationDate { get; init; }

    public required EstimateStatus Status { get; init; }

    public required string StatusLabel { get; init; }

    public required decimal Total { get; init; }

    public required string CurrencyCode { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class EstimateLineDto
{
    public required Guid Id { get; init; }

    public Guid? CatalogItemId { get; init; }

    public required string Description { get; init; }

    public required decimal Quantity { get; init; }

    public required CatalogUnitType Unit { get; init; }

    public required string UnitLabel { get; init; }

    public required decimal UnitPrice { get; init; }

    public required bool IsTaxable { get; init; }

    public required int SortOrder { get; init; }

    public required decimal LineAmount { get; init; }
}

public sealed class EstimateDetailsDto
{
    public required Guid Id { get; init; }

    public required int Sequence { get; init; }

    public required string Number { get; init; }

    public required Guid ClientId { get; init; }

    public required string ClientName { get; init; }

    public required bool ClientIsActive { get; init; }

    public required DateOnly IssueDate { get; init; }

    public DateOnly? ExpirationDate { get; init; }

    public required EstimateStatus Status { get; init; }

    public required string StatusLabel { get; init; }

    public string? Notes { get; init; }

    public string? Terms { get; init; }

    public required decimal Discount { get; init; }

    public required decimal TaxRatePercent { get; init; }

    public required decimal Subtotal { get; init; }

    public required decimal TaxableSubtotal { get; init; }

    public required decimal Tax { get; init; }

    public required decimal Total { get; init; }

    public required string CurrencyCode { get; init; }

    public required bool CanEdit { get; init; }

    public required IReadOnlyList<EstimateStatus> AllowedTransitions { get; init; }

    public required byte[] RowVersion { get; init; }

    public required IReadOnlyList<EstimateLineDto> Lines { get; init; }

    public static EstimateDetailsDto From(
        Estimate estimate,
        string clientName,
        bool clientIsActive,
        byte[]? rowVersion = null)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        byte[] token = rowVersion ?? estimate.RowVersion;

        return new EstimateDetailsDto
        {
            Id = estimate.Id,
            Sequence = estimate.Sequence,
            Number = estimate.Number,
            ClientId = estimate.ClientId,
            ClientName = clientName,
            ClientIsActive = clientIsActive,
            IssueDate = estimate.IssueDate,
            ExpirationDate = estimate.ExpirationDate,
            Status = estimate.Status,
            StatusLabel = EstimateStatusRules.Label(estimate.Status),
            Notes = estimate.Notes,
            Terms = estimate.Terms,
            Discount = estimate.Discount,
            TaxRatePercent = estimate.TaxRatePercent,
            Subtotal = estimate.Subtotal,
            TaxableSubtotal = estimate.TaxableSubtotal,
            Tax = estimate.Tax,
            Total = estimate.Total,
            CurrencyCode = estimate.Currency.Value,
            CanEdit = estimate.CanEdit,
            AllowedTransitions = EstimateStatusRules.UserFacingTargets(estimate.Status),
            RowVersion = [.. token],
            Lines = [.. estimate.Lines
                .OrderBy(line => line.SortOrder)
                .Select(line => new EstimateLineDto
                {
                    Id = line.Id,
                    CatalogItemId = line.CatalogItemId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    UnitLabel = CatalogUnitTypeDisplay.Label(line.Unit),
                    UnitPrice = line.UnitPrice,
                    IsTaxable = line.IsTaxable,
                    SortOrder = line.SortOrder,
                    LineAmount = line.LineAmount
                })]
        };
    }
}

public sealed class EstimateClientOption
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Code { get; init; }
}

public sealed class EstimateCatalogOption
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required CatalogUnitType Unit { get; init; }

    public required string UnitLabel { get; init; }

    public required decimal UnitPrice { get; init; }

    public required bool IsTaxable { get; init; }
}

public sealed class EstimateFormOptions
{
    public required IReadOnlyList<EstimateClientOption> Clients { get; init; }

    public required IReadOnlyList<EstimateCatalogOption> CatalogItems { get; init; }

    public required string CurrencyCode { get; init; }

    public required int DefaultPaymentTermsDays { get; init; }

    public string? DefaultNotes { get; init; }

    public required DateOnly Today { get; init; }
}

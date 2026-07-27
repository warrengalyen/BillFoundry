using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;

namespace BillFoundry.Application.Estimates;

public class SaveEstimateCommand
{
    public Guid ClientId { get; set; }

    public DateOnly IssueDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    public string? Notes { get; set; }

    public string? Terms { get; set; }

    public decimal Discount { get; set; }

    public decimal TaxRatePercent { get; set; }
}

public sealed class UpdateEstimateCommand : SaveEstimateCommand
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public class EstimateConcurrencyCommand
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public class SaveEstimateLineCommand : EstimateConcurrencyCommand
{
    public Guid? CatalogItemId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;

    public CatalogUnitType Unit { get; set; } = CatalogUnitType.Hour;

    public decimal UnitPrice { get; set; }

    public bool IsTaxable { get; set; }
}

public sealed class UpdateEstimateLineCommand : SaveEstimateLineCommand
{
    public Guid LineId { get; set; }
}

public sealed class RemoveEstimateLineCommand : EstimateConcurrencyCommand
{
    public Guid LineId { get; set; }
}

public sealed class ReorderEstimateLinesCommand : EstimateConcurrencyCommand
{
    public IReadOnlyList<Guid> LineIds { get; set; } = [];
}

public sealed class TransitionEstimateCommand : EstimateConcurrencyCommand
{
    public EstimateStatus Target { get; set; }
}

public sealed class DuplicateEstimateCommand
{
    public Guid Id { get; set; }
}

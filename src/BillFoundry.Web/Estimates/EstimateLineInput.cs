using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Estimates;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;

namespace BillFoundry.Web.Estimates;

public sealed class EstimateLineInput
{
    public Guid? CatalogItemId { get; set; }

    [Required]
    [StringLength(EstimateLine.DescriptionMaxLength)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.0001", "999999.9999")]
    public decimal Quantity { get; set; } = 1m;

    [Required]
    public CatalogUnitType Unit { get; set; } = CatalogUnitType.Hour;

    [Range(0, (double)EstimateLine.MaxUnitPrice)]
    public decimal UnitPrice { get; set; }

    public bool IsTaxable { get; set; }

    public void CopyFrom(EstimateLineDto line)
    {
        CatalogItemId = line.CatalogItemId;
        Description = line.Description;
        Quantity = line.Quantity;
        Unit = line.Unit;
        UnitPrice = line.UnitPrice;
        IsTaxable = line.IsTaxable;
    }

    public void ApplyCatalogItem(EstimateCatalogOption item)
    {
        CatalogItemId = item.Id;
        Description = string.IsNullOrWhiteSpace(item.Description) ? item.Name : item.Description;
        Unit = item.Unit;
        UnitPrice = item.UnitPrice;
        IsTaxable = item.IsTaxable;
        if (Quantity <= 0m)
        {
            Quantity = 1m;
        }
    }

    public SaveEstimateLineCommand ToAddCommand(Guid estimateId, byte[] rowVersion)
    {
        var command = new SaveEstimateLineCommand();
        CopyTo(command, estimateId, rowVersion);
        return command;
    }

    public UpdateEstimateLineCommand ToUpdateCommand(Guid estimateId, Guid lineId, byte[] rowVersion)
    {
        UpdateEstimateLineCommand command = new()
        {
            LineId = lineId
        };
        CopyTo(command, estimateId, rowVersion);
        return command;
    }

    private void CopyTo(SaveEstimateLineCommand command, Guid estimateId, byte[] rowVersion)
    {
        command.Id = estimateId;
        command.RowVersion = rowVersion;
        command.CatalogItemId = CatalogItemId;
        command.Description = Description;
        command.Quantity = Quantity;
        command.Unit = Unit;
        command.UnitPrice = UnitPrice;
        command.IsTaxable = IsTaxable;
    }
}

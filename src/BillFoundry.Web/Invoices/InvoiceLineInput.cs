using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Invoices;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;

namespace BillFoundry.Web.Invoices;

public sealed class InvoiceLineInput
{
    public Guid? CatalogItemId { get; set; }

    [Required]
    [StringLength(InvoiceLine.DescriptionMaxLength)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.0001", "999999.9999")]
    public decimal Quantity { get; set; } = 1m;

    [Required]
    public CatalogUnitType Unit { get; set; } = CatalogUnitType.Hour;

    [Range(0, (double)InvoiceLine.MaxUnitPrice)]
    public decimal UnitPrice { get; set; }

    public bool IsTaxable { get; set; }

    public void CopyFrom(InvoiceLineDto line)
    {
        CatalogItemId = line.CatalogItemId;
        Description = line.Description;
        Quantity = line.Quantity;
        Unit = line.Unit;
        UnitPrice = line.UnitPrice;
        IsTaxable = line.IsTaxable;
    }

    public void ApplyCatalogItem(InvoiceCatalogOption item)
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

    public SaveInvoiceLineCommand ToAddCommand(Guid invoiceId, byte[] rowVersion)
    {
        var command = new SaveInvoiceLineCommand();
        CopyTo(command, invoiceId, rowVersion);
        return command;
    }

    public UpdateInvoiceLineCommand ToUpdateCommand(Guid invoiceId, Guid lineId, byte[] rowVersion)
    {
        UpdateInvoiceLineCommand command = new()
        {
            LineId = lineId
        };
        CopyTo(command, invoiceId, rowVersion);
        return command;
    }

    private void CopyTo(SaveInvoiceLineCommand command, Guid invoiceId, byte[] rowVersion)
    {
        command.Id = invoiceId;
        command.RowVersion = rowVersion;
        command.CatalogItemId = CatalogItemId;
        command.Description = Description;
        command.Quantity = Quantity;
        command.Unit = Unit;
        command.UnitPrice = UnitPrice;
        command.IsTaxable = IsTaxable;
    }
}

using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Catalog;
using BillFoundry.Domain.Catalog;

namespace BillFoundry.Web.Catalog;

public sealed class CatalogItemInput
{
    [Required]
    [StringLength(CatalogItem.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(CatalogItem.DescriptionMaxLength)]
    public string? Description { get; set; }

    [StringLength(CatalogSku.MaxLength)]
    public string? Sku { get; set; }

    [Required]
    public CatalogUnitType UnitType { get; set; } = CatalogUnitType.Hour;

    [Range(0, (double)CatalogItem.MaxUnitPrice)]
    public decimal DefaultUnitPrice { get; set; }

    public bool IsTaxable { get; set; }

    public string RowVersionBase64 { get; set; } = string.Empty;

    public byte[] RowVersionBytes =>
        string.IsNullOrWhiteSpace(RowVersionBase64) ? [] : Convert.FromBase64String(RowVersionBase64);

    public void CopyFrom(CatalogItemDetailsDto item)
    {
        Name = item.Name;
        Description = item.Description;
        Sku = item.Sku;
        UnitType = item.UnitType;
        DefaultUnitPrice = item.DefaultUnitPrice;
        IsTaxable = item.IsTaxable;
        RowVersionBase64 = Convert.ToBase64String(item.RowVersion);
    }

    public SaveCatalogItemCommand ToCreateCommand() => ToSaveCommand();

    public UpdateCatalogItemCommand ToUpdateCommand(Guid id)
    {
        UpdateCatalogItemCommand command = new()
        {
            Id = id,
            RowVersion = RowVersionBytes
        };
        CopyTo(command);
        return command;
    }

    private SaveCatalogItemCommand ToSaveCommand()
    {
        var command = new SaveCatalogItemCommand();
        CopyTo(command);
        return command;
    }

    private void CopyTo(SaveCatalogItemCommand command)
    {
        command.Name = Name;
        command.Description = Description;
        command.Sku = Sku;
        command.UnitType = UnitType;
        command.DefaultUnitPrice = DefaultUnitPrice;
        command.IsTaxable = IsTaxable;
    }
}

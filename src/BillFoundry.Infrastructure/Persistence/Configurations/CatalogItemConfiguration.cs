using BillFoundry.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("CatalogItems", table =>
        {
            table.HasCheckConstraint(
                "CK_CatalogItems_UnitPrice",
                "[DefaultUnitPrice] >= 0");
            table.HasCheckConstraint(
                "CK_CatalogItems_UnitType",
                "[UnitType] IN ('Hour', 'Day', 'Item', 'FlatFee')");
        });

        builder.Property(item => item.Id)
            .ValueGeneratedNever();

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .HasMaxLength(CatalogItem.NameMaxLength)
            .IsRequired();

        builder.HasIndex(item => item.Name);

        builder.Property(item => item.Description)
            .HasMaxLength(CatalogItem.DescriptionMaxLength);

        builder.Property(item => item.Sku)
            .HasMaxLength(CatalogSku.MaxLength)
            .IsUnicode(false);

        builder.HasIndex(item => item.Sku)
            .IsUnique()
            .HasFilter("[Sku] IS NOT NULL")
            .HasDatabaseName("IX_CatalogItems_Sku");

        builder.Property(item => item.UnitType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(item => item.UnitType);

        builder.Property(item => item.DefaultUnitPrice)
            .HasPrecision(CatalogItem.PricePrecision, CatalogItem.PriceScale)
            .IsRequired();

        builder.Property(item => item.IsTaxable)
            .IsRequired();

        builder.Property(item => item.IsActive)
            .IsRequired();

        builder.HasIndex(item => item.IsActive);

        builder.Property(item => item.RowVersion)
            .IsRowVersion();

        builder.Property(item => item.CreatedAtUtc)
            .IsRequired();
    }
}

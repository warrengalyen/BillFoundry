using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines", table =>
        {
            table.HasCheckConstraint(
                "CK_InvoiceLines_Quantity",
                "[Quantity] > 0");
            table.HasCheckConstraint(
                "CK_InvoiceLines_UnitPrice",
                "[UnitPrice] >= 0");
            table.HasCheckConstraint(
                "CK_InvoiceLines_LineAmount",
                "[LineAmount] >= 0");
            table.HasCheckConstraint(
                "CK_InvoiceLines_Unit",
                "[Unit] IN ('Hour', 'Day', 'Item', 'FlatFee')");
            table.HasCheckConstraint(
                "CK_InvoiceLines_SortOrder",
                "[SortOrder] >= 0");
        });

        builder.Property(line => line.Id)
            .ValueGeneratedNever();

        builder.HasKey(line => line.Id);

        builder.Property(line => line.InvoiceId)
            .IsRequired();

        builder.HasIndex(line => new { line.InvoiceId, line.SortOrder })
            .IsUnique()
            .HasDatabaseName("IX_InvoiceLines_InvoiceId_SortOrder");

        builder.Property(line => line.Description)
            .HasMaxLength(InvoiceLine.DescriptionMaxLength)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasPrecision(CatalogItem.PricePrecision, CatalogItem.PriceScale)
            .IsRequired();

        builder.Property(line => line.Unit)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(line => line.UnitPrice)
            .HasPrecision(CatalogItem.PricePrecision, CatalogItem.PriceScale)
            .IsRequired();

        builder.Property(line => line.IsTaxable)
            .IsRequired();

        builder.Property(line => line.SortOrder)
            .IsRequired();

        builder.Property(line => line.LineAmount)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.HasOne<CatalogItem>()
            .WithMany()
            .HasForeignKey(line => line.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(line => line.CreatedAtUtc)
            .IsRequired();
    }
}

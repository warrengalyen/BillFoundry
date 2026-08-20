using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class EstimateLineConfiguration(RelationalSql sql) : IEntityTypeConfiguration<EstimateLine>
{
    public void Configure(EntityTypeBuilder<EstimateLine> builder)
    {
        builder.ToTable("EstimateLines", table =>
        {
            table.HasCheckConstraint(
                "CK_EstimateLines_Quantity",
                $"{sql.Ident("Quantity")} > 0");
            table.HasCheckConstraint(
                "CK_EstimateLines_UnitPrice",
                $"{sql.Ident("UnitPrice")} >= 0");
            table.HasCheckConstraint(
                "CK_EstimateLines_LineAmount",
                $"{sql.Ident("LineAmount")} >= 0");
            table.HasCheckConstraint(
                "CK_EstimateLines_Unit",
                $"{sql.Ident("Unit")} IN ('Hour', 'Day', 'Item', 'FlatFee')");
            table.HasCheckConstraint(
                "CK_EstimateLines_SortOrder",
                $"{sql.Ident("SortOrder")} >= 0");
        });

        builder.Property(line => line.Id)
            .ValueGeneratedNever();

        builder.HasKey(line => line.Id);

        builder.Property(line => line.EstimateId)
            .IsRequired();

        builder.HasIndex(line => new { line.EstimateId, line.SortOrder })
            .IsUnique()
            .HasDatabaseName("IX_EstimateLines_EstimateId_SortOrder");

        builder.Property(line => line.Description)
            .HasMaxLength(EstimateLine.DescriptionMaxLength)
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
            .HasPrecision(Estimate.AmountPrecision, Estimate.AmountScale)
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

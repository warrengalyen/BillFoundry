using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class EstimateConfiguration(RelationalSql sql) : IEntityTypeConfiguration<Estimate>
{
    public void Configure(EntityTypeBuilder<Estimate> builder)
    {
        builder.ToTable("Estimates", table =>
        {
            table.HasCheckConstraint(
                "CK_Estimates_Status",
                $"{sql.Ident("Status")} IN ('Draft', 'Sent', 'Accepted', 'Declined', 'Expired', 'Converted')");
            table.HasCheckConstraint(
                "CK_Estimates_Discount",
                $"{sql.Ident("Discount")} >= 0 AND {sql.Ident("Discount")} <= {sql.Ident("Subtotal")}");
            table.HasCheckConstraint(
                "CK_Estimates_TaxRate",
                $"{sql.Ident("TaxRatePercent")} >= 0 AND {sql.Ident("TaxRatePercent")} <= 100");
            table.HasCheckConstraint(
                "CK_Estimates_Expiration",
                $"{sql.Ident("ExpirationDate")} IS NULL OR {sql.Ident("ExpirationDate")} >= {sql.Ident("IssueDate")}");
        });

        builder.Property(estimate => estimate.Id)
            .ValueGeneratedNever();

        builder.HasKey(estimate => estimate.Id);

        builder.Property(estimate => estimate.Sequence)
            .IsRequired();

        builder.HasIndex(estimate => estimate.Sequence)
            .IsUnique();

        builder.Property(estimate => estimate.Number)
            .HasMaxLength(EstimateNumber.MaxLength)
            .IsRequired()
            .IsUnicode(false);

        builder.HasIndex(estimate => estimate.Number)
            .IsUnique();

        builder.Property(estimate => estimate.ClientId)
            .IsRequired();

        builder.HasIndex(estimate => estimate.ClientId);

        builder.HasOne<Domain.Clients.Client>()
            .WithMany()
            .HasForeignKey(estimate => estimate.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(estimate => estimate.IssueDate)
            .IsRequired();

        builder.HasIndex(estimate => estimate.IssueDate);

        builder.Property(estimate => estimate.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(estimate => estimate.Status);

        builder.Property(estimate => estimate.Notes)
            .HasMaxLength(Estimate.NotesMaxLength);

        builder.Property(estimate => estimate.Terms)
            .HasMaxLength(Estimate.TermsMaxLength);

        builder.Property(estimate => estimate.Discount)
            .HasPrecision(Estimate.AmountPrecision, Estimate.AmountScale)
            .IsRequired();

        builder.Property(estimate => estimate.TaxRatePercent)
            .HasPrecision(CatalogItem.PricePrecision, CatalogItem.PriceScale)
            .IsRequired();

        builder.Property(estimate => estimate.Subtotal)
            .HasPrecision(Estimate.AmountPrecision, Estimate.AmountScale)
            .IsRequired();

        builder.Property(estimate => estimate.TaxableSubtotal)
            .HasPrecision(Estimate.AmountPrecision, Estimate.AmountScale)
            .IsRequired();

        builder.Property(estimate => estimate.Tax)
            .HasPrecision(Estimate.AmountPrecision, Estimate.AmountScale)
            .IsRequired();

        builder.Property(estimate => estimate.Total)
            .HasPrecision(Estimate.AmountPrecision, Estimate.AmountScale)
            .IsRequired();

        builder.Property(estimate => estimate.Currency)
            .HasConversion(code => code.Value, value => CurrencyCode.Parse(value))
            .HasMaxLength(CurrencyCode.Length)
            .IsRequired()
            .IsUnicode(false);

        sql.ConfigureRowVersion(builder, estimate => estimate.RowVersion);

        builder.Property(estimate => estimate.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(estimate => estimate.Lines)
            .WithOne()
            .HasForeignKey(line => line.EstimateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(estimate => estimate.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

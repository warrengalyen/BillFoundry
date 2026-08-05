using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;
using BillFoundry.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", table =>
        {
            table.HasCheckConstraint(
                "CK_Invoices_Status",
                "[Status] IN ('Draft', 'Sent', 'PartiallyPaid', 'Paid', 'Overdue', 'Void')");
            table.HasCheckConstraint(
                "CK_Invoices_Discount",
                "[Discount] >= 0 AND [Discount] <= [Subtotal]");
            table.HasCheckConstraint(
                "CK_Invoices_TaxRate",
                "[TaxRatePercent] >= 0 AND [TaxRatePercent] <= 100");
            table.HasCheckConstraint(
                "CK_Invoices_DueDate",
                "[DueDate] >= [IssueDate]");
            table.HasCheckConstraint(
                "CK_Invoices_AmountPaid",
                "[AmountPaid] >= 0 AND [AmountPaid] <= [Total]");
            table.HasCheckConstraint(
                "CK_Invoices_BalanceDue",
                "[BalanceDue] >= 0");
        });

        builder.Property(invoice => invoice.Id)
            .ValueGeneratedNever();

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Sequence)
            .IsRequired();

        builder.HasIndex(invoice => invoice.Sequence)
            .IsUnique();

        builder.Property(invoice => invoice.Number)
            .HasMaxLength(InvoiceNumber.MaxLength)
            .IsRequired()
            .IsUnicode(false);

        builder.HasIndex(invoice => invoice.Number)
            .IsUnique();

        builder.Property(invoice => invoice.ClientId)
            .IsRequired();

        builder.HasIndex(invoice => invoice.ClientId);

        builder.HasOne<Domain.Clients.Client>()
            .WithMany()
            .HasForeignKey(invoice => invoice.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(invoice => invoice.ClientSnapshot, snapshot =>
        {
            snapshot.Property(value => value.Name)
                .HasMaxLength(Domain.Clients.Client.NameMaxLength)
                .IsRequired()
                .HasColumnName("ClientName");
            snapshot.Property(value => value.Code)
                .HasMaxLength(Domain.Clients.ClientCode.MaxLength)
                .IsRequired()
                .IsUnicode(false)
                .HasColumnName("ClientCode");
            snapshot.Property(value => value.Email)
                .HasMaxLength(Domain.Clients.Client.EmailMaxLength)
                .HasColumnName("ClientEmail");
        });
        builder.Navigation(invoice => invoice.ClientSnapshot).IsRequired();

        builder.Property(invoice => invoice.IssueDate)
            .IsRequired();

        builder.HasIndex(invoice => invoice.IssueDate);

        builder.Property(invoice => invoice.DueDate)
            .IsRequired();

        builder.HasIndex(invoice => invoice.DueDate);

        builder.Property(invoice => invoice.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(invoice => invoice.Status);

        builder.HasIndex(invoice => new { invoice.Status, invoice.DueDate })
            .IncludeProperties(invoice => invoice.BalanceDue)
            .HasDatabaseName("IX_Invoices_Status_DueDate");

        builder.Property(invoice => invoice.PurchaseOrder)
            .HasMaxLength(Invoice.PurchaseOrderMaxLength);

        builder.HasIndex(invoice => invoice.PurchaseOrder);

        builder.Property(invoice => invoice.Notes)
            .HasMaxLength(Invoice.NotesMaxLength);

        builder.Property(invoice => invoice.PaymentInstructions)
            .HasMaxLength(Invoice.PaymentInstructionsMaxLength);

        builder.Property(invoice => invoice.VoidReason)
            .HasMaxLength(Invoice.VoidReasonMaxLength);

        builder.Property(invoice => invoice.Discount)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.Property(invoice => invoice.TaxRatePercent)
            .HasPrecision(CatalogItem.PricePrecision, CatalogItem.PriceScale)
            .IsRequired();

        builder.Property(invoice => invoice.Subtotal)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.Property(invoice => invoice.TaxableSubtotal)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.Property(invoice => invoice.Tax)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.Property(invoice => invoice.Total)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.Property(invoice => invoice.AmountPaid)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.Property(invoice => invoice.BalanceDue)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.Property(invoice => invoice.Currency)
            .HasConversion(code => code.Value, value => CurrencyCode.Parse(value))
            .HasMaxLength(CurrencyCode.Length)
            .IsRequired()
            .IsUnicode(false);

        builder.HasIndex(invoice => invoice.SourceEstimateId)
            .IsUnique()
            .HasFilter("[SourceEstimateId] IS NOT NULL")
            .HasDatabaseName("IX_Invoices_SourceEstimateId");

        builder.HasOne<Domain.Estimates.Estimate>()
            .WithMany()
            .HasForeignKey(invoice => invoice.SourceEstimateId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(invoice => invoice.RowVersion)
            .IsRowVersion();

        builder.Property(invoice => invoice.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(invoice => invoice.Lines)
            .WithOne()
            .HasForeignKey(line => line.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(invoice => invoice.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(invoice => invoice.Payments)
            .WithOne()
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(invoice => invoice.Payments)
            .HasField("_payments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

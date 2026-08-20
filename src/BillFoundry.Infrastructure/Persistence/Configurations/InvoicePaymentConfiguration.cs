using BillFoundry.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class InvoicePaymentConfiguration(RelationalSql sql) : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.ToTable("InvoicePayments", table =>
        {
            table.HasCheckConstraint(
                "CK_InvoicePayments_Amount",
                $"{sql.Ident("Amount")} > 0");
            table.HasCheckConstraint(
                "CK_InvoicePayments_Method",
                $"{sql.Ident("Method")} IN ('Cash', 'Check', 'BankTransfer', 'CreditCard', 'PayPal', 'Other')");
            table.HasCheckConstraint(
                "CK_InvoicePayments_Reversal",
                $"({sql.Ident("ReversesPaymentId")} IS NULL AND {sql.Ident("ReversalReason")} IS NULL) OR ({sql.Ident("ReversesPaymentId")} IS NOT NULL AND {sql.Ident("ReversalReason")} IS NOT NULL)");
        });

        builder.Property(payment => payment.Id)
            .ValueGeneratedNever();

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.InvoiceId)
            .IsRequired();

        builder.HasIndex(payment => payment.InvoiceId);

        builder.Property(payment => payment.PaymentDate)
            .IsRequired();

        builder.HasIndex(payment => new { payment.InvoiceId, payment.PaymentDate });

        builder.HasIndex(payment => payment.PaymentDate)
            .HasDatabaseName("IX_InvoicePayments_PaymentDate");

        builder.Property(payment => payment.Amount)
            .HasPrecision(Invoice.AmountPrecision, Invoice.AmountScale)
            .IsRequired();

        builder.Property(payment => payment.Method)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(payment => payment.Reference)
            .HasMaxLength(InvoicePayment.ReferenceMaxLength);

        builder.Property(payment => payment.Notes)
            .HasMaxLength(InvoicePayment.NotesMaxLength);

        builder.HasIndex(payment => payment.ReversesPaymentId)
            .IsUnique()
            .HasFilter(sql.IsNotNull("ReversesPaymentId"))
            .HasDatabaseName("IX_InvoicePayments_ReversesPaymentId");

        builder.HasOne<InvoicePayment>()
            .WithMany()
            .HasForeignKey(payment => payment.ReversesPaymentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(payment => payment.ReversalReason)
            .HasMaxLength(InvoicePayment.ReversalReasonMaxLength);

        builder.Property(payment => payment.CreatedAtUtc)
            .IsRequired();
    }
}

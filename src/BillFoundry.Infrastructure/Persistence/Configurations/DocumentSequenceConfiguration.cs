using BillFoundry.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("DocumentSequences", table =>
        {
            table.HasCheckConstraint(
                "CK_DocumentSequences_NextValue",
                "[NextValue] >= 1");
        });

        builder.HasKey(sequence => sequence.Kind);

        builder.Property(sequence => sequence.Kind)
            .HasMaxLength(DocumentSequence.KindMaxLength)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(sequence => sequence.NextValue)
            .IsRequired();

        builder.HasData(
            DocumentSequence.Create(DocumentSequence.EstimateKind),
            DocumentSequence.Create(DocumentSequence.InvoiceKind));
    }
}

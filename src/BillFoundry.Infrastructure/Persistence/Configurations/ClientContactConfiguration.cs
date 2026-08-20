using BillFoundry.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class ClientContactConfiguration(RelationalSql sql) : IEntityTypeConfiguration<ClientContact>
{
    public void Configure(EntityTypeBuilder<ClientContact> builder)
    {
        builder.ToTable("ClientContacts");

        builder.Property(contact => contact.Id)
            .ValueGeneratedNever();

        builder.HasKey(contact => contact.Id);

        builder.Property(contact => contact.ClientId)
            .IsRequired();

        builder.Property(contact => contact.Name)
            .HasMaxLength(ClientContact.NameMaxLength)
            .IsRequired();

        builder.Property(contact => contact.JobTitle)
            .HasMaxLength(ClientContact.JobTitleMaxLength);

        builder.Property(contact => contact.Email)
            .HasMaxLength(ClientContact.EmailMaxLength);

        builder.Property(contact => contact.Phone)
            .HasMaxLength(ClientContact.PhoneMaxLength);

        builder.Property(contact => contact.IsPrimary)
            .IsRequired();

        builder.Property(contact => contact.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(contact => new { contact.ClientId, contact.Name })
            .HasDatabaseName("IX_ClientContacts_ClientId_Name");

        builder.HasIndex(contact => new { contact.ClientId, contact.IsPrimary })
            .IsUnique()
            .HasFilter($"{sql.Ident("IsPrimary")} = {sql.TrueLiteral}")
            .HasDatabaseName("IX_ClientContacts_PrimaryPerClient");
    }
}

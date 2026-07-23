using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.Property(client => client.Id)
            .ValueGeneratedNever();

        builder.HasKey(client => client.Id);

        builder.Property(client => client.Number)
            .IsRequired();

        builder.HasIndex(client => client.Number)
            .IsUnique();

        builder.Property(client => client.Code)
            .HasMaxLength(ClientCode.MaxLength)
            .IsRequired()
            .IsUnicode(false);

        builder.HasIndex(client => client.Code)
            .IsUnique();

        builder.Property(client => client.Name)
            .HasMaxLength(Client.NameMaxLength)
            .IsRequired();

        builder.HasIndex(client => client.Name);

        builder.Property(client => client.Email)
            .HasMaxLength(Client.EmailMaxLength);

        builder.HasIndex(client => client.Email);

        builder.Property(client => client.Phone)
            .HasMaxLength(Client.PhoneMaxLength);

        builder.Property(client => client.Website)
            .HasMaxLength(Client.WebsiteMaxLength);

        builder.OwnsOne(client => client.BillingAddress, address =>
        {
            address.Property(value => value.Line1)
                .HasMaxLength(PostalAddress.LineMaxLength)
                .HasColumnName("AddressLine1");
            address.Property(value => value.Line2)
                .HasMaxLength(PostalAddress.LineMaxLength)
                .HasColumnName("AddressLine2");
            address.Property(value => value.City)
                .HasMaxLength(PostalAddress.CityMaxLength)
                .HasColumnName("City");
            address.Property(value => value.Region)
                .HasMaxLength(PostalAddress.RegionMaxLength)
                .HasColumnName("Region");
            address.Property(value => value.PostalCode)
                .HasMaxLength(PostalAddress.PostalCodeMaxLength)
                .HasColumnName("PostalCode");
            address.Property(value => value.Country)
                .HasMaxLength(PostalAddress.CountryMaxLength)
                .HasColumnName("Country");
        });
        builder.Navigation(client => client.BillingAddress).IsRequired(false);

        builder.Property(client => client.Notes)
            .HasMaxLength(Client.NotesMaxLength);

        builder.Property(client => client.IsActive)
            .IsRequired();

        builder.HasIndex(client => client.IsActive);

        builder.Property(client => client.RowVersion)
            .IsRowVersion();

        builder.Property(client => client.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(client => client.Contacts)
            .WithOne()
            .HasForeignKey(contact => contact.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(client => client.Contacts)
            .HasField("_contacts")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

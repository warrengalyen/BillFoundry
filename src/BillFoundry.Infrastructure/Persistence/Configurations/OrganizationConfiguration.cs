using BillFoundry.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration(RelationalSql sql) : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Organizations_SingletonId",
                $"{sql.Ident("Id")} = '{Organization.SingletonId}'");
            table.HasCheckConstraint(
                "CK_Organizations_PaymentTerms",
                $"{sql.Ident("DefaultPaymentTermsDays")} >= {Organization.MinPaymentTermsDays} AND {sql.Ident("DefaultPaymentTermsDays")} <= {Organization.MaxPaymentTermsDays}");
        });

        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.LegalName)
            .HasMaxLength(Organization.NameMaxLength)
            .IsRequired();

        builder.Property(organization => organization.DisplayName)
            .HasMaxLength(Organization.NameMaxLength)
            .IsRequired();

        builder.OwnsOne(organization => organization.Address, address =>
        {
            address.Property(value => value.Line1)
                .HasMaxLength(PostalAddress.LineMaxLength)
                .IsRequired()
                .HasColumnName("AddressLine1");
            address.Property(value => value.Line2)
                .HasMaxLength(PostalAddress.LineMaxLength)
                .HasColumnName("AddressLine2");
            address.Property(value => value.City)
                .HasMaxLength(PostalAddress.CityMaxLength)
                .IsRequired()
                .HasColumnName("City");
            address.Property(value => value.Region)
                .HasMaxLength(PostalAddress.RegionMaxLength)
                .HasColumnName("Region");
            address.Property(value => value.PostalCode)
                .HasMaxLength(PostalAddress.PostalCodeMaxLength)
                .HasColumnName("PostalCode");
            address.Property(value => value.Country)
                .HasMaxLength(PostalAddress.CountryMaxLength)
                .IsRequired()
                .HasColumnName("Country");
        });
        builder.Navigation(organization => organization.Address).IsRequired();

        builder.Property(organization => organization.Email)
            .HasMaxLength(Organization.EmailMaxLength);

        builder.Property(organization => organization.Phone)
            .HasMaxLength(Organization.PhoneMaxLength);

        builder.Property(organization => organization.Website)
            .HasMaxLength(Organization.WebsiteMaxLength);

        builder.Property(organization => organization.TaxIdentifier)
            .HasMaxLength(Organization.TaxIdentifierMaxLength);

        builder.Property(organization => organization.DefaultCurrency)
            .HasConversion(code => code.Value, value => CurrencyCode.Parse(value))
            .HasMaxLength(CurrencyCode.Length)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(organization => organization.DefaultPaymentTermsDays)
            .IsRequired();

        builder.Property(organization => organization.DefaultInvoicePrefix)
            .HasConversion(prefix => prefix.Value, value => DocumentPrefix.Parse(value))
            .HasMaxLength(DocumentPrefix.MaxLength)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(organization => organization.DefaultEstimatePrefix)
            .HasConversion(prefix => prefix.Value, value => DocumentPrefix.Parse(value))
            .HasMaxLength(DocumentPrefix.MaxLength)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(organization => organization.DefaultInvoiceNotes)
            .HasMaxLength(Organization.NotesMaxLength);

        builder.Property(organization => organization.DefaultPaymentInstructions)
            .HasMaxLength(Organization.NotesMaxLength);

        builder.OwnsOne(organization => organization.Logo, logo =>
        {
            logo.Property(value => value.StoredFileName)
                .HasMaxLength(OrganizationLogo.StoredFileNameMaxLength)
                .HasColumnName("LogoStoredFileName");
            logo.Property(value => value.ContentType)
                .HasMaxLength(OrganizationLogo.ContentTypeMaxLength)
                .HasColumnName("LogoContentType");
            logo.Property(value => value.SizeBytes)
                .HasColumnName("LogoSizeBytes");
        });

        sql.ConfigureRowVersion(builder, organization => organization.RowVersion);

        builder.Property(organization => organization.CreatedAtUtc)
            .IsRequired();
    }
}

using BillFoundry.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.IsDisabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(user => user.IsDisabled);
    }
}

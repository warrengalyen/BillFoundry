using BillFoundry.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration(RelationalSql sql) : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id)
            .ValueGeneratedNever();

        builder.Property(audit => audit.OccurredAtUtc)
            .IsRequired();

        builder.HasIndex(audit => audit.OccurredAtUtc)
            .HasDatabaseName("IX_AuditEvents_OccurredAtUtc");

        builder.Property(audit => audit.UserName)
            .HasMaxLength(AuditEvent.UserNameMaxLength);

        builder.HasIndex(audit => audit.UserId)
            .HasDatabaseName("IX_AuditEvents_UserId");

        builder.Property(audit => audit.Action)
            .HasMaxLength(AuditEvent.ActionMaxLength)
            .IsRequired()
            .IsUnicode(false);

        builder.HasIndex(audit => audit.Action)
            .HasDatabaseName("IX_AuditEvents_Action");

        builder.Property(audit => audit.EntityType)
            .HasMaxLength(AuditEvent.EntityTypeMaxLength)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(audit => audit.EntityId);

        builder.HasIndex(audit => new { audit.EntityType, audit.EntityId, audit.OccurredAtUtc })
            .HasDatabaseName("IX_AuditEvents_EntityType_EntityId_OccurredAtUtc");

        builder.Property(audit => audit.Description)
            .HasMaxLength(AuditEvent.DescriptionMaxLength)
            .IsRequired();

        if (!sql.IsPostgreSql)
        {
            builder.Property(audit => audit.MetadataJson)
                .HasColumnType("nvarchar(max)");
        }
    }
}

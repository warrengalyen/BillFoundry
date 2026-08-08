using BillFoundry.Domain.Auditing;

namespace BillFoundry.Domain.Tests;

public sealed class AuditEventTests
{
    [Fact]
    public void Create_stores_business_fields_and_truncates_description()
    {
        DateTimeOffset occurred = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);
        Guid userId = Guid.NewGuid();
        Guid entityId = Guid.NewGuid();
        string longDescription = new('a', AuditEvent.DescriptionMaxLength + 25);

        AuditEvent audit = AuditEvent.Create(
            occurred,
            userId,
            "admin@localhost",
            "InvoiceSent",
            "Invoice",
            entityId,
            longDescription,
            """{"amount":"40.00"}""");

        Assert.NotEqual(Guid.Empty, audit.Id);
        Assert.Equal(occurred, audit.OccurredAtUtc);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal("admin@localhost", audit.UserName);
        Assert.Equal("InvoiceSent", audit.Action);
        Assert.Equal("Invoice", audit.EntityType);
        Assert.Equal(entityId, audit.EntityId);
        Assert.Equal(AuditEvent.DescriptionMaxLength, audit.Description.Length);
        Assert.Equal("""{"amount":"40.00"}""", audit.MetadataJson);
    }

    [Fact]
    public void Create_rejects_blank_action_and_description()
    {
        DateTimeOffset occurred = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(
            occurred, null, null, " ", "Invoice", Guid.NewGuid(), "Created invoice INV-0001.", null));
        Assert.Throws<ArgumentException>(() => AuditEvent.Create(
            occurred, null, null, "InvoiceCreated", "Invoice", Guid.NewGuid(), "  ", null));
    }
}

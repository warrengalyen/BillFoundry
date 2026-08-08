using BillFoundry.Application.Auditing;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Invoices;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class AuditPersistenceTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public AuditPersistenceTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Audit-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Invoice_send_and_payment_write_audit_rows_in_the_same_save()
    {
        ClaimsPrincipalCurrentUser actor = OrganizationTestHost.Administrator();
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, actor);
        (IInvoiceService invoices, IAuditService audit, Guid clientId) = await SeedAsync(provider);

        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, "Work", 1m, 40m));
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));
        InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = lined.Invoice!.Id,
            RowVersion = lined.Invoice.RowVersion
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));
        InvoiceResult paid = await invoices.RecordPaymentAsync(new RecordPaymentCommand
        {
            Id = sent.Invoice!.Id,
            RowVersion = sent.Invoice.RowVersion,
            PaymentDate = sent.Invoice.IssueDate,
            Amount = 40m,
            Method = PaymentMethod.Check,
            Reference = "CHK-40"
        });
        Assert.True(paid.Succeeded, string.Join("; ", paid.Errors));

        AuditQueryResult<IReadOnlyList<AuditEventDto>> timeline =
            await audit.ListForEntityAsync(AuditEntityTypes.Invoice, sent.Invoice.Id);
        Assert.True(timeline.Succeeded);
        IReadOnlyList<AuditEventDto> events = timeline.Value!;
        Assert.Contains(events, item => item.Action == AuditActions.InvoiceCreated);
        Assert.Contains(events, item => item.Action == AuditActions.InvoiceSent);
        Assert.Contains(events, item => item.Action == AuditActions.PaymentRecorded);
        Assert.Contains(events, item => item.Description.Contains(sent.Invoice.Number, StringComparison.Ordinal));
        Assert.All(events, item => Assert.Equal(actor.UserId, item.UserId));
        Assert.DoesNotContain(events, item =>
            item.Metadata.ContainsKey("password")
            || item.Description.Contains("CHK-40", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Failed_send_does_not_persist_an_invoice_sent_audit()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        (IInvoiceService invoices, IAuditService audit, Guid clientId) = await SeedAsync(provider);

        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, "Work", 1m, 25m));
        byte[] stale = lined.Invoice!.RowVersion;
        InvoiceResult updated = await invoices.UpdateHeaderAsync(new UpdateInvoiceCommand
        {
            Id = lined.Invoice.Id,
            RowVersion = lined.Invoice.RowVersion,
            ClientId = clientId,
            IssueDate = lined.Invoice.IssueDate,
            DueDate = lined.Invoice.DueDate,
            Notes = $"{_marker} bumped"
        });
        Assert.True(updated.Succeeded, string.Join("; ", updated.Errors));

        InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = lined.Invoice.Id,
            RowVersion = stale
        });
        Assert.True(sent.IsConcurrencyConflict);

        AuditQueryResult<IReadOnlyList<AuditEventDto>> timeline =
            await audit.ListForEntityAsync(AuditEntityTypes.Invoice, lined.Invoice.Id);
        Assert.True(timeline.Succeeded);
        Assert.DoesNotContain(timeline.Value!, item => item.Action == AuditActions.InvoiceSent);

        await using var db = CreateDb();
        Assert.Equal(0, await db.AuditEvents.CountAsync(item =>
            item.EntityId == lined.Invoice.Id && item.Action == AuditActions.InvoiceSent));
    }

    [Fact]
    public async Task Sensitive_metadata_is_not_stored()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IAuditRecorder recorder = provider.GetRequiredService<IAuditRecorder>();
        IAuditService audit = provider.GetRequiredService<IAuditService>();
        Guid entityId = Guid.NewGuid();

        recorder.Record(new AuditWriteRequest
        {
            Action = AuditActions.PasswordChanged,
            EntityType = AuditEntityTypes.User,
            EntityId = entityId,
            Description = "Changed the account password.",
            Metadata = new Dictionary<string, string?>
            {
                ["password"] = "SuperSecret123!",
                ["token"] = "reset-token-value",
                ["reason"] = "user requested"
            }
        });
        await recorder.PersistAsync();

        AuditQueryResult<IReadOnlyList<AuditEventDto>> timeline =
            await audit.ListForEntityAsync(AuditEntityTypes.User, entityId);
        Assert.True(timeline.Succeeded);
        AuditEventDto item = Assert.Single(timeline.Value!);
        Assert.False(item.Metadata.ContainsKey("password"));
        Assert.False(item.Metadata.ContainsKey("token"));
        Assert.Equal("user requested", item.Metadata["reason"]);
        Assert.DoesNotContain("SuperSecret123!", item.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("reset-token-value", string.Join(',', item.Metadata.Values), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_requires_administrator_while_invoice_timeline_allows_invoice_managers()
    {
        await using ServiceProvider userProvider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser());
        IAuditService userAudit = userProvider.GetRequiredService<IAuditService>();
        IInvoiceService invoices = userProvider.GetRequiredService<IInvoiceService>();
        IClientService clients = userProvider.GetRequiredService<IClientService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Client" });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        InvoiceResult created = await invoices.CreateAsync(Header(client.Client!.Id));
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));

        AuditQueryResult<AuditSearchResult> search = await userAudit.SearchAsync(new AuditSearchQuery());
        Assert.True(search.IsForbidden);

        AuditQueryResult<IReadOnlyList<AuditEventDto>> timeline =
            await userAudit.ListForEntityAsync(AuditEntityTypes.Invoice, created.Invoice!.Id);
        Assert.True(timeline.Succeeded);
        Assert.Contains(timeline.Value!, item => item.Action == AuditActions.InvoiceCreated);

        AuditQueryResult<IReadOnlyList<AuditEventDto>> organization =
            await userAudit.ListForEntityAsync(AuditEntityTypes.Organization, Guid.NewGuid());
        Assert.True(organization.IsForbidden);
    }

    private async Task<(IInvoiceService Invoices, IAuditService Audit, Guid ClientId)> SeedAsync(IServiceProvider provider)
    {
        IClientService clients = provider.GetRequiredService<IClientService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Client" });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        return (
            provider.GetRequiredService<IInvoiceService>(),
            provider.GetRequiredService<IAuditService>(),
            client.Client!.Id);
    }

    private SaveInvoiceCommand Header(Guid clientId) =>
        new()
        {
            ClientId = clientId,
            IssueDate = new DateOnly(2026, 8, 22),
            DueDate = new DateOnly(2026, 9, 21),
            Notes = _marker
        };

    private static SaveInvoiceLineCommand Line(InvoiceDetailsDto invoice, string description, decimal quantity, decimal unitPrice) =>
        new()
        {
            Id = invoice.Id,
            RowVersion = invoice.RowVersion,
            Description = description,
            Quantity = quantity,
            Unit = CatalogUnitType.Hour,
            UnitPrice = unitPrice,
            IsTaxable = false
        };

    private BillFoundryDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(_sql.ConnectionString)
            .Options);
}

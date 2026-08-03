using BillFoundry.Application.Catalog;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Documents;
using BillFoundry.Application.Invoices;
using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Invoices;
using BillFoundry.Infrastructure.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

public sealed class InvoiceDocumentGeneratorTests
{
    [Fact]
    public void Generate_writes_persisted_invoice_fields()
    {
        var generator = new PdfInvoiceDocumentGenerator();
        InvoiceDocumentModel model = SampleInvoice();

        GeneratedDocument document = generator.Generate(model);
        string text = PdfText.Read(document.Content);

        Assert.Equal("invoice-INV-0042.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(document.Content, 0, 4), StringComparison.Ordinal);
        Assert.Contains("Invoice", text, StringComparison.Ordinal);
        Assert.Contains("INV-0042", text, StringComparison.Ordinal);
        Assert.Contains("Sent", text, StringComparison.Ordinal);
        Assert.Contains("August 22, 2026", text, StringComparison.Ordinal);
        Assert.Contains("September 21, 2026", text, StringComparison.Ordinal);
        Assert.Contains("PO-99", text, StringComparison.Ordinal);
        Assert.Contains("Northwind Traders", text, StringComparison.Ordinal);
        Assert.Contains("NW-1", text, StringComparison.Ordinal);
        Assert.Contains("Acme LLC", text, StringComparison.Ordinal);
        Assert.Contains("Website design", text, StringComparison.Ordinal);
        Assert.Contains("Hour", text, StringComparison.Ordinal);
        Assert.Contains("USD 1,250.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 2,500.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 100.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 240.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 2,640.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 400.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 2,240.00", text, StringComparison.Ordinal);
        Assert.Contains("Please pay by bank transfer.", text, StringComparison.Ordinal);
        Assert.Contains("Thank you for your business.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/App_Data", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_marks_void_invoices()
    {
        var generator = new PdfInvoiceDocumentGenerator();
        InvoiceDocumentModel model = SampleInvoice() with { StatusLabel = "Void", IsVoid = true, AmountPaid = 0m, BalanceDue = 0m };

        string text = PdfText.Read(generator.Generate(model).Content);

        Assert.Contains("VOID", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_omits_an_unreadable_logo_and_still_produces_a_pdf()
    {
        var generator = new PdfInvoiceDocumentGenerator();
        InvoiceDocumentModel model = SampleInvoice() with
        {
            Issuer = SampleInvoice().Issuer with { LogoBytes = [0x00, 0x01, 0x02, 0x03] }
        };

        GeneratedDocument document = generator.Generate(model);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(document.Content, 0, 4), StringComparison.Ordinal);
        Assert.Contains("INV-0042", PdfText.Read(document.Content), StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_rejects_a_null_model()
    {
        var generator = new PdfInvoiceDocumentGenerator();
        Assert.Throws<ArgumentNullException>(() => generator.Generate(null!));
    }

    private static InvoiceDocumentModel SampleInvoice() =>
        new()
        {
            Issuer = new DocumentIssuerModel
            {
                LegalName = "Acme LLC",
                DisplayName = "Acme",
                Email = "billing@acme.test",
                Phone = "555-0100",
                Website = "https://acme.test",
                TaxId = "12-3456789",
                AddressLines = ["10 Main St", "Springfield IL 62701", "United States"]
            },
            Client = new DocumentPartyModel
            {
                Name = "Northwind Traders",
                Code = "NW-1",
                Email = "ap@northwind.test",
                Phone = "555-0199",
                AddressLines = ["1 Harbor Way", "Seattle WA 98101"]
            },
            Number = "INV-0042",
            StatusLabel = "Sent",
            IssueDate = new DateOnly(2026, 8, 22),
            DueDate = new DateOnly(2026, 9, 21),
            PurchaseOrder = "PO-99",
            CurrencyCode = "USD",
            Lines =
            [
                new DocumentLineModel
                {
                    Description = "Website design",
                    Quantity = 2m,
                    UnitLabel = "Hour",
                    UnitPrice = 1250m,
                    LineAmount = 2500m
                }
            ],
            Subtotal = 2500m,
            Discount = 100m,
            TaxRatePercent = 10m,
            Tax = 240m,
            Total = 2640m,
            AmountPaid = 400m,
            BalanceDue = 2240m,
            Notes = "Thank you for your business.",
            PaymentInstructions = "Please pay by bank transfer.",
            IsVoid = false
        };
}

[Collection(SqlServerCollection.Name)]
public sealed class InvoiceDocumentServiceTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public InvoiceDocumentServiceTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Pdf-Inv-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Service_prints_persisted_amounts_after_catalog_prices_change()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        await SeedLetterheadAsync(provider);
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        ICatalogService catalog = provider.GetRequiredService<ICatalogService>();
        IInvoiceDocumentService documents = provider.GetRequiredService<IInvoiceDocumentService>();

        CatalogItemResult item = await catalog.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Hour",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 100m,
            IsTaxable = false
        });
        Assert.True(item.Succeeded, string.Join("; ", item.Errors));

        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        InvoiceResult lined = await invoices.AddLineAsync(new SaveInvoiceLineCommand
        {
            Id = created.Invoice!.Id,
            RowVersion = created.Invoice.RowVersion,
            CatalogItemId = item.Item!.Id,
            Description = "Original snapshot",
            Quantity = 2m,
            Unit = CatalogUnitType.Hour,
            UnitPrice = 100m,
            IsTaxable = false
        });
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));

        CatalogItemResult updated = await catalog.UpdateAsync(new UpdateCatalogItemCommand
        {
            Id = item.Item.Id,
            RowVersion = item.Item.RowVersion,
            Name = $"{_marker} Hour",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 999m,
            IsTaxable = false
        });
        Assert.True(updated.Succeeded, string.Join("; ", updated.Errors));

        DocumentResult pdf = await documents.GenerateAsync(created.Invoice.Id);
        Assert.True(pdf.Succeeded, string.Join("; ", pdf.Errors));
        string text = PdfText.Read(pdf.Document!.Content);

        Assert.Contains(created.Invoice.Number, text, StringComparison.Ordinal);
        Assert.Contains("Original snapshot", text, StringComparison.Ordinal);
        Assert.Contains("USD 100.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 200.00", text, StringComparison.Ordinal);
        Assert.DoesNotContain("999.00", text, StringComparison.Ordinal);
        Assert.Equal(DocumentFileName.ForInvoice(created.Invoice.Number), pdf.Document.FileName);
        Assert.Equal("application/pdf", pdf.Document.ContentType);
        Assert.Contains("Acme LLC", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_prints_amount_paid_and_balance_due()
    {
        var clock = new FixedDateTimeProvider(new DateOnly(2026, 8, 22));
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            clock);
        await SeedLetterheadAsync(provider);
        (IInvoiceService invoices, Guid clientId) = await SeedClientAsync(provider);
        IInvoiceDocumentService documents = provider.GetRequiredService<IInvoiceDocumentService>();

        InvoiceResult created = await invoices.CreateAsync(Header(clientId));
        InvoiceResult lined = await invoices.AddLineAsync(Line(created.Invoice!, "Work", 1m, 100m, false));
        InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = lined.Invoice!.Id,
            RowVersion = lined.Invoice.RowVersion
        });
        InvoiceResult paid = await invoices.RecordPaymentAsync(new RecordPaymentCommand
        {
            Id = sent.Invoice!.Id,
            RowVersion = sent.Invoice.RowVersion,
            PaymentDate = sent.Invoice.IssueDate,
            Amount = 40m,
            Method = PaymentMethod.Check,
            Reference = "CHK-9"
        });
        Assert.True(paid.Succeeded, string.Join("; ", paid.Errors));

        DocumentResult pdf = await documents.GenerateAsync(sent.Invoice.Id);
        Assert.True(pdf.Succeeded, string.Join("; ", pdf.Errors));
        string text = PdfText.Read(pdf.Document!.Content);

        Assert.Contains("USD 40.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 60.00", text, StringComparison.Ordinal);
        Assert.Contains("Amount paid", text, StringComparison.Ordinal);
        Assert.Contains("Balance due", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_generate_an_invoice_pdf()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        IInvoiceDocumentService documents = provider.GetRequiredService<IInvoiceDocumentService>();

        DocumentResult result = await documents.GenerateAsync(Guid.NewGuid());

        Assert.True(result.IsForbidden);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task Missing_invoice_returns_not_found()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser());
        IInvoiceDocumentService documents = provider.GetRequiredService<IInvoiceDocumentService>();

        DocumentResult result = await documents.GenerateAsync(Guid.NewGuid());

        Assert.True(result.IsNotFound);
        Assert.Null(result.Document);
    }

    private async Task SeedLetterheadAsync(IServiceProvider provider)
    {
        IOrganizationSettingsService settings = provider.GetRequiredService<IOrganizationSettingsService>();
        OrganizationSettingsResult current = await settings.GetAsync();
        Assert.True(current.Succeeded, string.Join("; ", current.Errors));
        OrganizationSettingsResult updated = await settings.UpdateAsync(
            OrganizationTestHost.ValidCommand(current.Organization!.RowVersion));
        Assert.True(updated.Succeeded, string.Join("; ", updated.Errors));
    }

    private async Task<(IInvoiceService Invoices, Guid ClientId)> SeedClientAsync(IServiceProvider provider)
    {
        IClientService clients = provider.GetRequiredService<IClientService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand
        {
            Name = $"{_marker} Client",
            Email = "ap@client.test",
            Phone = "555-0142",
            AddressLine1 = "9 Market Street",
            City = "Portland",
            Region = "OR",
            PostalCode = "97201",
            Country = "United States"
        });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        return (invoices, client.Client!.Id);
    }

    private SaveInvoiceCommand Header(Guid clientId) =>
        new()
        {
            ClientId = clientId,
            IssueDate = new DateOnly(2026, 8, 22),
            DueDate = new DateOnly(2026, 9, 21),
            PurchaseOrder = "PO-PDF",
            Notes = $"{_marker} notes",
            PaymentInstructions = "Pay by transfer."
        };

    private static SaveInvoiceLineCommand Line(
        InvoiceDetailsDto invoice,
        string description,
        decimal quantity,
        decimal unitPrice,
        bool taxable) =>
        new()
        {
            Id = invoice.Id,
            RowVersion = invoice.RowVersion,
            Description = description,
            Quantity = quantity,
            Unit = CatalogUnitType.Hour,
            UnitPrice = unitPrice,
            IsTaxable = taxable
        };

    private sealed class FixedDateTimeProvider(DateOnly today) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}

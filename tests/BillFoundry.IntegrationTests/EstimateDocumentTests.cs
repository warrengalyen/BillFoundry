using BillFoundry.Application.Catalog;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Documents;
using BillFoundry.Application.Estimates;
using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using BillFoundry.Infrastructure.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

public sealed class EstimateDocumentGeneratorTests
{
    [Fact]
    public void Generate_writes_persisted_estimate_fields()
    {
        var generator = new PdfEstimateDocumentGenerator();
        EstimateDocumentModel model = new()
        {
            Issuer = new DocumentIssuerModel
            {
                LegalName = "Acme LLC",
                DisplayName = "Acme",
                Email = "billing@acme.test",
                Phone = "555-0100",
                AddressLines = ["10 Main St", "Springfield IL 62701"]
            },
            Client = new DocumentPartyModel
            {
                Name = "Northwind Traders",
                Code = "NW-1",
                Email = "ap@northwind.test"
            },
            Number = "EST-0007",
            StatusLabel = "Sent",
            IssueDate = new DateOnly(2026, 8, 22),
            ExpirationDate = new DateOnly(2026, 9, 30),
            CurrencyCode = "USD",
            Lines =
            [
                new DocumentLineModel
                {
                    Description = "Discovery workshop",
                    Quantity = 8m,
                    UnitLabel = "Hour",
                    UnitPrice = 150m,
                    LineAmount = 1200m
                }
            ],
            Subtotal = 1200m,
            Discount = 50m,
            TaxRatePercent = 10m,
            Tax = 115m,
            Total = 1265m,
            Notes = "Valid while scheduled.",
            Terms = "Net 15 if accepted."
        };

        GeneratedDocument document = generator.Generate(model);
        string text = PdfText.Read(document.Content);

        Assert.Equal("estimate-EST-0007.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(document.Content, 0, 4), StringComparison.Ordinal);
        Assert.Contains("Estimate", text, StringComparison.Ordinal);
        Assert.Contains("EST-0007", text, StringComparison.Ordinal);
        Assert.Contains("September 30, 2026", text, StringComparison.Ordinal);
        Assert.Contains("Discovery workshop", text, StringComparison.Ordinal);
        Assert.Contains("USD 1,200.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 1,265.00", text, StringComparison.Ordinal);
        Assert.Contains("Net 15 if accepted.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Amount paid", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Balance due", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_rejects_a_null_model()
    {
        var generator = new PdfEstimateDocumentGenerator();
        Assert.Throws<ArgumentNullException>(() => generator.Generate(null!));
    }
}

[Collection(SqlServerCollection.Name)]
public sealed class EstimateDocumentServiceTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public EstimateDocumentServiceTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Pdf-Est-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Service_prints_persisted_amounts_after_catalog_prices_change()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        await SeedLetterheadAsync(provider);
        (IEstimateService estimates, Guid clientId) = await SeedClientAsync(provider);
        ICatalogService catalog = provider.GetRequiredService<ICatalogService>();
        IEstimateDocumentService documents = provider.GetRequiredService<IEstimateDocumentService>();

        CatalogItemResult item = await catalog.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{_marker} Hour",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 80m,
            IsTaxable = false
        });
        Assert.True(item.Succeeded, string.Join("; ", item.Errors));

        EstimateResult created = await estimates.CreateAsync(Header(clientId));
        EstimateResult lined = await estimates.AddLineAsync(new SaveEstimateLineCommand
        {
            Id = created.Estimate!.Id,
            RowVersion = created.Estimate.RowVersion,
            CatalogItemId = item.Item!.Id,
            Description = "Estimate snapshot",
            Quantity = 3m,
            Unit = CatalogUnitType.Hour,
            UnitPrice = 80m,
            IsTaxable = false
        });
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));

        CatalogItemResult updated = await catalog.UpdateAsync(new UpdateCatalogItemCommand
        {
            Id = item.Item.Id,
            RowVersion = item.Item.RowVersion,
            Name = $"{_marker} Hour",
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 400m,
            IsTaxable = false
        });
        Assert.True(updated.Succeeded, string.Join("; ", updated.Errors));

        DocumentResult pdf = await documents.GenerateAsync(created.Estimate.Id);
        Assert.True(pdf.Succeeded, string.Join("; ", pdf.Errors));
        string text = PdfText.Read(pdf.Document!.Content);

        Assert.Contains(created.Estimate.Number, text, StringComparison.Ordinal);
        Assert.Contains("Estimate snapshot", text, StringComparison.Ordinal);
        Assert.Contains("USD 80.00", text, StringComparison.Ordinal);
        Assert.Contains("USD 240.00", text, StringComparison.Ordinal);
        Assert.DoesNotContain("400.00", text, StringComparison.Ordinal);
        Assert.Equal(DocumentFileName.ForEstimate(created.Estimate.Number), pdf.Document.FileName);
        Assert.Contains("Acme LLC", text, StringComparison.Ordinal);
        Assert.Contains("Net 30 on acceptance.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_generate_an_estimate_pdf()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        IEstimateDocumentService documents = provider.GetRequiredService<IEstimateDocumentService>();

        DocumentResult result = await documents.GenerateAsync(Guid.NewGuid());

        Assert.True(result.IsForbidden);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task Missing_estimate_returns_not_found()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser());
        IEstimateDocumentService documents = provider.GetRequiredService<IEstimateDocumentService>();

        DocumentResult result = await documents.GenerateAsync(Guid.NewGuid());

        Assert.True(result.IsNotFound);
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

    private async Task<(IEstimateService Estimates, Guid ClientId)> SeedClientAsync(IServiceProvider provider)
    {
        IClientService clients = provider.GetRequiredService<IClientService>();
        IEstimateService estimates = provider.GetRequiredService<IEstimateService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand
        {
            Name = $"{_marker} Client",
            Email = "hello@client.test",
            AddressLine1 = "4 Pine Street",
            City = "Seattle",
            Region = "WA",
            PostalCode = "98101",
            Country = "United States"
        });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        return (estimates, client.Client!.Id);
    }

    private SaveEstimateCommand Header(Guid clientId) =>
        new()
        {
            ClientId = clientId,
            IssueDate = new DateOnly(2026, 8, 22),
            ExpirationDate = new DateOnly(2026, 9, 30),
            Notes = $"{_marker} notes",
            Terms = "Net 30 on acceptance."
        };
}

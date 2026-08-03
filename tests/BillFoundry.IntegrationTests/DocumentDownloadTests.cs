using System.Net;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Documents;
using BillFoundry.Application.Estimates;
using BillFoundry.Application.Invoices;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

public sealed class DocumentDownloadAnonymousTests : IClassFixture<BillFoundryWebApplicationFactory>
{
    private readonly BillFoundryWebApplicationFactory _factory;

    public DocumentDownloadAnonymousTests(BillFoundryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Invoice_pdf_redirects_unauthenticated_users_to_login()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/Invoices/{Guid.NewGuid()}/pdf", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Estimate_pdf_redirects_unauthenticated_users_to_login()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/Estimates/{Guid.NewGuid()}/pdf", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}

[Collection(SqlServerCollection.Name)]
public sealed class DocumentDownloadTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public DocumentDownloadTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Pdf-Http-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Authenticated_user_can_download_an_invoice_pdf()
    {
        Guid invoiceId;
        string number;
        await using (ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser()))
        {
            (IInvoiceService invoices, Guid clientId) = await SeedInvoiceClientAsync(provider);
            InvoiceResult created = await invoices.CreateAsync(new SaveInvoiceCommand
            {
                ClientId = clientId,
                IssueDate = new DateOnly(2026, 8, 22),
                DueDate = new DateOnly(2026, 9, 21),
                Notes = _marker
            });
            Assert.True(created.Succeeded, string.Join("; ", created.Errors));
            InvoiceResult lined = await invoices.AddLineAsync(new SaveInvoiceLineCommand
            {
                Id = created.Invoice!.Id,
                RowVersion = created.Invoice.RowVersion,
                Description = "Downloadable work",
                Quantity = 1m,
                Unit = CatalogUnitType.Hour,
                UnitPrice = 75m,
                IsTaxable = false
            });
            Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));
            invoiceId = created.Invoice.Id;
            number = created.Invoice.Number;
        }

        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/Invoices/{invoiceId}/pdf", UriKind.Relative));
        byte[] body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            DocumentFileName.ForInvoice(number),
            response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName,
            StringComparison.Ordinal);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(body, 0, 4), StringComparison.Ordinal);
        Assert.Contains(number, PdfText.Read(body), StringComparison.Ordinal);
        Assert.DoesNotContain(_sql.LogoRoot, PdfText.Read(body), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authenticated_user_can_download_an_estimate_pdf()
    {
        Guid estimateId;
        string number;
        await using (ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser()))
        {
            IClientService clients = provider.GetRequiredService<IClientService>();
            IEstimateService estimates = provider.GetRequiredService<IEstimateService>();
            ClientResult createdClient = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Client" });
            Assert.True(createdClient.Succeeded, string.Join("; ", createdClient.Errors));
            EstimateResult created = await estimates.CreateAsync(new SaveEstimateCommand
            {
                ClientId = createdClient.Client!.Id,
                IssueDate = new DateOnly(2026, 8, 22),
                Terms = "Quoted terms."
            });
            Assert.True(created.Succeeded, string.Join("; ", created.Errors));
            EstimateResult lined = await estimates.AddLineAsync(new SaveEstimateLineCommand
            {
                Id = created.Estimate!.Id,
                RowVersion = created.Estimate.RowVersion,
                Description = "Quoted work",
                Quantity = 1m,
                Unit = CatalogUnitType.Hour,
                UnitPrice = 90m,
                IsTaxable = false
            });
            Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));
            estimateId = created.Estimate.Id;
            number = created.Estimate.Number;
        }

        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/Estimates/{estimateId}/pdf", UriKind.Relative));
        byte[] body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            DocumentFileName.ForEstimate(number),
            response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName,
            StringComparison.Ordinal);
        Assert.Contains(number, PdfText.Read(body), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_invoice_pdf_returns_not_found()
    {
        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/Invoices/{Guid.NewGuid()}/pdf", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(IInvoiceService Invoices, Guid ClientId)> SeedInvoiceClientAsync(IServiceProvider provider)
    {
        IClientService clients = provider.GetRequiredService<IClientService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Client" });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));
        return (invoices, client.Client!.Id);
    }
}

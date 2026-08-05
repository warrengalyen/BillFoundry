using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Invoices;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

public sealed class ReportExportAnonymousTests : IClassFixture<BillFoundryWebApplicationFactory>
{
    private readonly BillFoundryWebApplicationFactory _factory;

    public ReportExportAnonymousTests(BillFoundryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Aging_csv_redirects_anonymous_users_to_login()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Reports/aging.csv", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}

[Collection(SqlServerCollection.Name)]
public sealed class ReportExportTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public ReportExportTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"RptHttp-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Authenticated_user_can_download_outstanding_csv()
    {
        await using (ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser()))
        {
            IClientService clients = provider.GetRequiredService<IClientService>();
            IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
            ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{_marker} Client" });
            Assert.True(client.Succeeded);
            InvoiceResult created = await invoices.CreateAsync(new SaveInvoiceCommand
            {
                ClientId = client.Client!.Id,
                IssueDate = new DateOnly(2026, 8, 1),
                DueDate = new DateOnly(2026, 8, 20),
                Notes = _marker
            });
            Assert.True(created.Succeeded);
            InvoiceResult lined = await invoices.AddLineAsync(new SaveInvoiceLineCommand
            {
                Id = created.Invoice!.Id,
                RowVersion = created.Invoice.RowVersion,
                Description = "Work",
                Quantity = 1m,
                Unit = CatalogUnitType.Hour,
                UnitPrice = 33m,
                IsTaxable = false
            });
            Assert.True(lined.Succeeded);
            InvoiceResult sent = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
            {
                Id = lined.Invoice!.Id,
                RowVersion = lined.Invoice.RowVersion
            });
            Assert.True(sent.Succeeded);
        }

        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient http = factory.CreateClient();
        http.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        http.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        http.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await http.GetAsync(new Uri("/Reports/outstanding.csv", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        ContentDispositionHeaderValue? disposition = response.Content.Headers.ContentDisposition;
        string? fileName = disposition?.FileNameStar ?? disposition?.FileName;
        Assert.NotNull(fileName);
        Assert.EndsWith(".csv", fileName.Trim('"'), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFileName(fileName.Trim('"')), fileName.Trim('"'));
        string body = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("InvoiceNumber", body, StringComparison.Ordinal);
        Assert.Contains(_marker, body, StringComparison.Ordinal);
    }
}

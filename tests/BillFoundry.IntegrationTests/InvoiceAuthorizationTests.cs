using System.Net;
using BillFoundry.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BillFoundry.IntegrationTests;

public sealed class InvoiceAuthorizationTests : IClassFixture<BillFoundryWebApplicationFactory>
{
    private readonly BillFoundryWebApplicationFactory _factory;

    public InvoiceAuthorizationTests(BillFoundryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Invoices_page_redirects_unauthenticated_users_to_login()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Invoices", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}

[Collection(SqlServerCollection.Name)]
public sealed class InvoicePageTests
{
    private readonly SqlServerFixture _sql;

    public InvoicePageTests(SqlServerFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task Authenticated_user_can_open_the_invoice_list()
    {
        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Invoices", UriKind.Relative));
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Skip to content", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Invoices</h1>", html, StringComparison.Ordinal);
        Assert.Contains("New invoice", html, StringComparison.Ordinal);
    }
}

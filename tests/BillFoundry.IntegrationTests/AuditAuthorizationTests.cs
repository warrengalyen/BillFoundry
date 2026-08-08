using System.Net;
using BillFoundry.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BillFoundry.IntegrationTests;

public sealed class AuditAuthorizationTests : IClassFixture<BillFoundryWebApplicationFactory>
{
    private readonly BillFoundryWebApplicationFactory _factory;

    public AuditAuthorizationTests(BillFoundryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Audit_page_redirects_unauthenticated_users_to_login()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Audit", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AuditAuthenticatedAuthorizationTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public AuditAuthenticatedAuthorizationTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_role_is_denied_the_audit_log()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Audit", UriKind.Relative));

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Forbidden,
            $"Unexpected status {response.StatusCode}");
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/Account/AccessDenied", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        }
    }
}

[Collection(SqlServerCollection.Name)]
public sealed class AuditPageTests
{
    private readonly SqlServerFixture _sql;

    public AuditPageTests(SqlServerFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task Administrator_can_open_the_audit_log()
    {
        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.Administrator);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Audit", UriKind.Relative));
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Skip to content", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Audit log</h1>", html, StringComparison.Ordinal);
        Assert.Contains("These records cannot be edited.", html, StringComparison.Ordinal);
    }
}

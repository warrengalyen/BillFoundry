using System.Net;
using BillFoundry.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BillFoundry.IntegrationTests;

public sealed class EstimateAuthorizationTests : IClassFixture<BillFoundryWebApplicationFactory>
{
    private readonly BillFoundryWebApplicationFactory _factory;

    public EstimateAuthorizationTests(BillFoundryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Estimates_page_redirects_unauthenticated_users_to_login()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Estimates", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}

[Collection(SqlServerCollection.Name)]
public sealed class EstimatePageTests
{
    private readonly SqlServerFixture _sql;

    public EstimatePageTests(SqlServerFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task Authenticated_user_can_open_the_estimate_list()
    {
        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Estimates", UriKind.Relative));
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Skip to content", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Estimates</h1>", html, StringComparison.Ordinal);
        Assert.Contains("New estimate", html, StringComparison.Ordinal);
    }
}

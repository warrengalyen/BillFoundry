using System.Net;
using BillFoundry.Application.Organizations;
using BillFoundry.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

public sealed class OrganizationSettingsAuthorizationTests : IClassFixture<BillFoundryWebApplicationFactory>
{
    private readonly BillFoundryWebApplicationFactory _factory;

    public OrganizationSettingsAuthorizationTests(BillFoundryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Organization_settings_redirects_unauthenticated_users_to_login()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Settings/Organization", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Organization_logo_redirects_unauthenticated_users_to_login()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/media/organization-logo", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class OrganizationSettingsAuthenticatedAuthorizationTests : IClassFixture<AuthenticatedWebApplicationFactory>
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    public OrganizationSettingsAuthenticatedAuthorizationTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_role_is_denied_organization_settings()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Settings/Organization", UriKind.Relative));

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Forbidden,
            $"Unexpected status {response.StatusCode}");
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/Account/AccessDenied", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task User_role_is_denied_organization_logo()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "user@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.User);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/media/organization-logo", UriKind.Relative));

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Forbidden,
            $"Unexpected status {response.StatusCode}");
    }
}

[Collection(SqlServerCollection.Name)]
public sealed class OrganizationSettingsPageTests
{
    private readonly SqlServerFixture _sql;

    public OrganizationSettingsPageTests(SqlServerFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task Administrator_can_open_organization_settings()
    {
        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.Administrator);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Settings/Organization", UriKind.Relative));
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Skip to content", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Organization</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Legal name", html, StringComparison.Ordinal);
        Assert.Contains("Default payment terms", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrator_can_download_uploaded_logo()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();
        OrganizationSettingsResult current = await service.GetAsync();
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        await using MemoryStream logo = new(png);
        OrganizationSettingsResult uploaded = await service.UploadLogoAsync(logo, current.Organization!.RowVersion);
        Assert.True(uploaded.Succeeded, string.Join("; ", uploaded.Errors));

        await using var factory = new SqlAuthenticatedWebApplicationFactory(_sql);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@localhost");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.Administrator);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/media/organization-logo", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        byte[] downloaded = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(png, downloaded);
    }
}

internal sealed class SqlAuthenticatedWebApplicationFactory : AuthenticatedWebApplicationFactory
{
    private readonly SqlServerFixture _sql;

    public SqlAuthenticatedWebApplicationFactory(SqlServerFixture sql)
    {
        _sql = sql;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:BillFoundry", _sql.ConnectionString);
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting("OrganizationLogoStorage:RootPath", _sql.LogoRoot);
        builder.UseSetting("IdentitySeed:Enabled", "false");
    }
}

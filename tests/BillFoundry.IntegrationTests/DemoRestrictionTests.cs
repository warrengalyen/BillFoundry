using System.Net;
using BillFoundry.Application.Organizations;
using BillFoundry.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class DemoOrganizationRestrictionTests
{
    private readonly SqlServerFixture _sql;

    public DemoOrganizationRestrictionTests(SqlServerFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task Organization_mutations_are_forbidden_when_demo_mode_is_on()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(
            _sql,
            OrganizationTestHost.Administrator(),
            extra: new Dictionary<string, string?> { ["DemoMode:Enabled"] = "true" });
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();

        OrganizationSettingsResult current = await service.GetAsync();
        Assert.True(current.Succeeded);

        OrganizationSettingsResult updated = await service.UpdateAsync(
            OrganizationTestHost.ValidCommand(current.Organization!.RowVersion));
        Assert.True(updated.IsForbidden);

        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        await using MemoryStream logo = new(png);
        OrganizationSettingsResult uploaded = await service.UploadLogoAsync(logo, current.Organization.RowVersion);
        Assert.True(uploaded.IsForbidden);
    }
}

public sealed class DemoPageRestrictionTests : IClassFixture<DemoModeWebApplicationFactory>
{
    private readonly DemoModeWebApplicationFactory _factory;

    public DemoPageRestrictionTests(DemoModeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Landing_page_identifies_the_installation_as_a_demo()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative));
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("demonstration", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fictional", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("admin@northbeacon.example", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forgot_password_is_not_available_in_demo_mode()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/Account/ForgotPassword", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DemoAuthenticatedRestrictionTests : IClassFixture<DemoModeAuthenticatedWebApplicationFactory>
{
    private readonly DemoModeAuthenticatedWebApplicationFactory _factory;

    public DemoAuthenticatedRestrictionTests(DemoModeAuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Change_password_is_denied_in_demo_mode()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@northbeacon.example");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, AppRoles.Administrator);

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/Account/Manage/ChangePassword", UriKind.Relative));

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Forbidden,
            $"Unexpected status {response.StatusCode}");
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains(
                "/Account/AccessDenied",
                response.Headers.Location?.OriginalString,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

public sealed class DemoModeWebApplicationFactory : BillFoundryWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("DemoMode:Enabled", "true");
    }
}

public sealed class DemoModeAuthenticatedWebApplicationFactory : AuthenticatedWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("DemoMode:Enabled", "true");
    }
}

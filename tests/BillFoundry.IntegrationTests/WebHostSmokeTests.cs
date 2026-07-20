using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BillFoundry.IntegrationTests;

public sealed class WebHostSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebHostSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body.Trim());
    }

    [Fact]
    public async Task Dashboard_includes_skip_to_content_and_heading()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Skip to content", html, StringComparison.Ordinal);
        Assert.Contains("id=\"main-content\"", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Dashboard</h1>", html, StringComparison.Ordinal);
    }
}

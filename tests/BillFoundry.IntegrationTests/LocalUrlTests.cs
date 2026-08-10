using BillFoundry.Web.Security;

namespace BillFoundry.IntegrationTests;

public sealed class LocalUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Invoices")]
    [InlineData("/Account/Manage")]
    [InlineData("~/")]
    [InlineData("~/Invoices")]
    public void Resolve_accepts_local_paths(string returnUrl)
    {
        Assert.Equal(returnUrl, LocalUrl.Resolve(returnUrl));
        Assert.True(LocalUrl.IsSafe(returnUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("~//evil.example")]
    public void Resolve_rejects_non_local_urls(string? returnUrl)
    {
        Assert.Equal("/", LocalUrl.Resolve(returnUrl));
        Assert.False(LocalUrl.IsSafe(returnUrl));
    }

    [Fact]
    public void Resolve_rejects_control_characters()
    {
        string tabUrl = "/\t/evil.example";
        string crUrl = "/\r/evil.example";

        Assert.Equal("/", LocalUrl.Resolve(tabUrl));
        Assert.Equal("/", LocalUrl.Resolve(crUrl));
        Assert.False(LocalUrl.IsSafe(tabUrl));
        Assert.False(LocalUrl.IsSafe(crUrl));
    }
}

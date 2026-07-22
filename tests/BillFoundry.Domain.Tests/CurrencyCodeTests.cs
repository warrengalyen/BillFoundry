using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Tests;

public sealed class CurrencyCodeTests
{
    [Theory]
    [InlineData("usd", "USD")]
    [InlineData("EUR", "EUR")]
    [InlineData(" Gbp ", "GBP")]
    public void TryParse_accepts_supported_codes(string input, string expected)
    {
        Assert.True(CurrencyCode.TryParse(input, out CurrencyCode code));
        Assert.Equal(expected, code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData("XXX")]
    public void TryParse_rejects_unsupported_codes(string? input)
    {
        Assert.False(CurrencyCode.TryParse(input, out _));
    }

    [Fact]
    public void Parse_throws_for_unknown_currency()
    {
        Assert.Throws<ArgumentException>(() => CurrencyCode.Parse("XXX"));
    }
}

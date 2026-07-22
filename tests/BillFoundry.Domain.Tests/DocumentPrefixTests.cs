using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Tests;

public sealed class DocumentPrefixTests
{
    [Theory]
    [InlineData("INV", "INV")]
    [InlineData("est", "EST")]
    [InlineData("A1", "A1")]
    [InlineData("Invoice12", "INVOICE12")]
    public void TryCreate_accepts_letter_then_alphanumeric(string input, string expected)
    {
        Assert.True(DocumentPrefix.TryCreate(input, out DocumentPrefix prefix));
        Assert.Equal(expected, prefix.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1INV")]
    [InlineData("IN-1")]
    [InlineData("TOO-LONG-PREFIX")]
    [InlineData("ABCDEFGHIJK")]
    public void TryCreate_rejects_invalid_prefixes(string? input)
    {
        Assert.False(DocumentPrefix.TryCreate(input, out _));
    }
}

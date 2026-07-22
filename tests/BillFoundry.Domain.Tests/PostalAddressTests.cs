using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Tests;

public sealed class PostalAddressTests
{
    [Fact]
    public void Create_requires_line_city_and_country()
    {
        PostalAddress address = PostalAddress.Create(
            "  10 Main St ",
            "Suite 4",
            " Springfield ",
            "IL",
            "62701",
            "United States");

        Assert.Equal("10 Main St", address.Line1);
        Assert.Equal("Suite 4", address.Line2);
        Assert.Equal("Springfield", address.City);
        Assert.Equal("IL", address.Region);
        Assert.Equal("62701", address.PostalCode);
        Assert.Equal("United States", address.Country);
    }

    [Theory]
    [InlineData(null, "City", "US")]
    [InlineData("Line", null, "US")]
    [InlineData("Line", "City", null)]
    [InlineData(" ", "City", "US")]
    public void Create_throws_when_required_parts_are_missing(string? line1, string? city, string? country)
    {
        Assert.Throws<ArgumentException>(() => PostalAddress.Create(line1, null, city, null, null, country));
    }

    [Fact]
    public void Create_allows_optional_region_and_postal_code()
    {
        PostalAddress address = PostalAddress.Create("1 Harbour Rd", null, "Singapore", null, null, "Singapore");

        Assert.Null(address.Line2);
        Assert.Null(address.Region);
        Assert.Null(address.PostalCode);
    }
}

namespace BillFoundry.Domain.Organizations;

/// <summary>
/// A postal mailing address stored on the organization profile.
/// </summary>
public sealed record PostalAddress
{
    public const int LineMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int RegionMaxLength = 100;
    public const int PostalCodeMaxLength = 20;
    public const int CountryMaxLength = 100;

    public PostalAddress(
        string line1,
        string? line2,
        string city,
        string? region,
        string? postalCode,
        string country)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        Region = region;
        PostalCode = postalCode;
        Country = country;
    }

    public string Line1 { get; }

    public string? Line2 { get; }

    public string City { get; }

    public string? Region { get; }

    public string? PostalCode { get; }

    public string Country { get; }

    public static PostalAddress Empty { get; } = new(string.Empty, null, string.Empty, null, null, string.Empty);

    public static PostalAddress Create(
        string? line1,
        string? line2,
        string? city,
        string? region,
        string? postalCode,
        string? country)
    {
        return new PostalAddress(
            OrganizationText.Required(line1, nameof(Line1), LineMaxLength),
            OrganizationText.Optional(line2, nameof(Line2), LineMaxLength),
            OrganizationText.Required(city, nameof(City), CityMaxLength),
            OrganizationText.Optional(region, nameof(Region), RegionMaxLength),
            OrganizationText.Optional(postalCode, nameof(PostalCode), PostalCodeMaxLength),
            OrganizationText.Required(country, nameof(Country), CountryMaxLength));
    }
}

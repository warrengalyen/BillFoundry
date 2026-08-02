using BillFoundry.Application.Documents;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Infrastructure.Pdf;

internal static class DocumentLayout
{
    public static DocumentIssuerModel Issuer(Organization? organization, byte[]? logoBytes)
    {
        if (organization is null)
        {
            return new DocumentIssuerModel
            {
                LegalName = string.Empty,
                DisplayName = string.Empty,
                LogoBytes = logoBytes
            };
        }

        return new DocumentIssuerModel
        {
            LegalName = organization.LegalName,
            DisplayName = organization.DisplayName,
            Email = organization.Email,
            Phone = organization.Phone,
            Website = organization.Website,
            TaxId = organization.TaxIdentifier,
            AddressLines = AddressLines(organization.Address),
            LogoBytes = logoBytes
        };
    }

    public static DocumentPartyModel Party(
        string name,
        string? code,
        string? email,
        string? phone,
        PostalAddress? address) =>
        new()
        {
            Name = name,
            Code = code,
            Email = email,
            Phone = phone,
            AddressLines = AddressLines(address)
        };

    public static DocumentLineModel Line(
        string description,
        decimal quantity,
        CatalogUnitType unit,
        decimal unitPrice,
        decimal lineAmount) =>
        new()
        {
            Description = description,
            Quantity = quantity,
            UnitLabel = CatalogUnitTypeDisplay.Label(unit),
            UnitPrice = unitPrice,
            LineAmount = lineAmount
        };

    public static IReadOnlyList<string> AddressLines(PostalAddress? address)
    {
        if (address is null)
        {
            return [];
        }

        var lines = new List<string>();
        Add(lines, address.Line1);
        Add(lines, address.Line2);

        string cityLine = string.Join(
            " ",
            new[] { address.City, address.Region, address.PostalCode }
                .Where(static part => !string.IsNullOrWhiteSpace(part)));
        Add(lines, cityLine);
        Add(lines, address.Country);
        return lines;
    }

    private static void Add(List<string> lines, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add(value.Trim());
        }
    }
}

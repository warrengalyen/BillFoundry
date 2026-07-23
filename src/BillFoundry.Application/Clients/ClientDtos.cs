using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Application.Clients;

public sealed class ClientListItemDto
{
    public required Guid Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public required bool IsActive { get; init; }

    public string? PrimaryContactName { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class ClientContactDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? JobTitle { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public required bool IsPrimary { get; init; }
}

public sealed class ClientDetailsDto
{
    public required Guid Id { get; init; }

    public required int Number { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Website { get; init; }

    public string? AddressLine1 { get; init; }

    public string? AddressLine2 { get; init; }

    public string? City { get; init; }

    public string? Region { get; init; }

    public string? PostalCode { get; init; }

    public string? Country { get; init; }

    public string? Notes { get; init; }

    public required bool IsActive { get; init; }

    public required byte[] RowVersion { get; init; }

    public required IReadOnlyList<ClientContactDto> Contacts { get; init; }

    public static ClientDetailsDto From(Client client, byte[]? rowVersion = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        PostalAddress? address = client.BillingAddress;
        byte[] token = rowVersion ?? client.RowVersion;

        return new ClientDetailsDto
        {
            Id = client.Id,
            Number = client.Number,
            Code = client.Code,
            Name = client.Name,
            Email = client.Email,
            Phone = client.Phone,
            Website = client.Website,
            AddressLine1 = address?.Line1,
            AddressLine2 = address?.Line2,
            City = address?.City,
            Region = address?.Region,
            PostalCode = address?.PostalCode,
            Country = address?.Country,
            Notes = client.Notes,
            IsActive = client.IsActive,
            RowVersion = [.. token],
            Contacts = client.Contacts
                .OrderByDescending(contact => contact.IsPrimary)
                .ThenBy(contact => contact.Name, StringComparer.OrdinalIgnoreCase)
                .Select(contact => new ClientContactDto
                {
                    Id = contact.Id,
                    Name = contact.Name,
                    JobTitle = contact.JobTitle,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    IsPrimary = contact.IsPrimary
                })
                .ToList()
        };
    }
}

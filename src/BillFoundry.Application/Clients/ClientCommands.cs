namespace BillFoundry.Application.Clients;

public class SaveClientCommand
{
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? Notes { get; set; }
}

public sealed class UpdateClientCommand : SaveClientCommand
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public class SaveContactCommand
{
    public Guid ClientId { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string Name { get; set; } = string.Empty;

    public string? JobTitle { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsPrimary { get; set; }
}

public sealed class UpdateContactCommand : SaveContactCommand
{
    public Guid ContactId { get; set; }
}

public class ClientConcurrencyCommand
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed class RemoveContactCommand : ClientConcurrencyCommand
{
    public Guid ContactId { get; set; }
}

public sealed class SetPrimaryContactCommand : ClientConcurrencyCommand
{
    public Guid ContactId { get; set; }
}

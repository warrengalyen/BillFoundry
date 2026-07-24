using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Clients;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Web.Clients;

public sealed class ClientInput
{
    [StringLength(ClientCode.MaxLength)]
    public string? Code { get; set; }

    [Required]
    [StringLength(Client.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(Client.EmailMaxLength)]
    public string? Email { get; set; }

    [StringLength(Client.PhoneMaxLength)]
    public string? Phone { get; set; }

    [StringLength(Client.WebsiteMaxLength)]
    public string? Website { get; set; }

    [StringLength(PostalAddress.LineMaxLength)]
    public string? AddressLine1 { get; set; }

    [StringLength(PostalAddress.LineMaxLength)]
    public string? AddressLine2 { get; set; }

    [StringLength(PostalAddress.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(PostalAddress.RegionMaxLength)]
    public string? Region { get; set; }

    [StringLength(PostalAddress.PostalCodeMaxLength)]
    public string? PostalCode { get; set; }

    [StringLength(PostalAddress.CountryMaxLength)]
    public string? Country { get; set; }

    [StringLength(Client.NotesMaxLength)]
    public string? Notes { get; set; }

    public string RowVersionBase64 { get; set; } = string.Empty;

    public byte[] RowVersionBytes =>
        string.IsNullOrWhiteSpace(RowVersionBase64) ? [] : Convert.FromBase64String(RowVersionBase64);

    public void CopyFrom(ClientDetailsDto client)
    {
        Code = client.Code;
        Name = client.Name;
        Email = client.Email;
        Phone = client.Phone;
        Website = client.Website;
        AddressLine1 = client.AddressLine1;
        AddressLine2 = client.AddressLine2;
        City = client.City;
        Region = client.Region;
        PostalCode = client.PostalCode;
        Country = client.Country;
        Notes = client.Notes;
        RowVersionBase64 = Convert.ToBase64String(client.RowVersion);
    }

    public SaveClientCommand ToCreateCommand() => ToSaveCommand();

    public UpdateClientCommand ToUpdateCommand(Guid id)
    {
        UpdateClientCommand command = new()
        {
            Id = id,
            RowVersion = RowVersionBytes
        };
        CopyTo(command);
        return command;
    }

    private SaveClientCommand ToSaveCommand()
    {
        var command = new SaveClientCommand();
        CopyTo(command);
        return command;
    }

    private void CopyTo(SaveClientCommand command)
    {
        command.Code = Code;
        command.Name = Name;
        command.Email = Email;
        command.Phone = Phone;
        command.Website = Website;
        command.AddressLine1 = AddressLine1;
        command.AddressLine2 = AddressLine2;
        command.City = City;
        command.Region = Region;
        command.PostalCode = PostalCode;
        command.Country = Country;
        command.Notes = Notes;
    }
}

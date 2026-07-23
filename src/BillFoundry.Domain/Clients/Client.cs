using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Clients;

/// <summary>
/// A customer of the installation. Clients are deactivated rather than permanently deleted
/// so later financial records can keep a stable reference.
/// </summary>
public sealed class Client : IAuditable
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 50;
    public const int WebsiteMaxLength = 200;
    public const int NotesMaxLength = 4000;

    private readonly List<ClientContact> _contacts = [];

    private Client()
    {
        Code = string.Empty;
        Name = string.Empty;
        RowVersion = [];
    }

    public Guid Id { get; private set; }

    public int Number { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Website { get; private set; }

    public PostalAddress? BillingAddress { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public IReadOnlyList<ClientContact> Contacts => _contacts;

    public ClientContact? PrimaryContact => _contacts.SingleOrDefault(contact => contact.IsPrimary);

    public static Client Create(
        int number,
        ClientCode code,
        string name,
        string? email,
        string? phone,
        string? website,
        PostalAddress? billingAddress,
        string? notes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

        var client = new Client
        {
            Id = Guid.NewGuid(),
            Number = number,
            IsActive = true
        };
        client.ApplyProfile(code, name, email, phone, website, billingAddress, notes);
        return client;
    }

    public void Update(
        ClientCode code,
        string name,
        string? email,
        string? phone,
        string? website,
        PostalAddress? billingAddress,
        string? notes) =>
        ApplyProfile(code, name, email, phone, website, billingAddress, notes);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public ClientContact AddContact(string name, string? jobTitle, string? email, string? phone, bool isPrimary)
    {
        bool makePrimary = isPrimary || _contacts.Count == 0;
        if (makePrimary)
        {
            ClearPrimary();
        }

        ClientContact contact = ClientContact.Create(Id, name, jobTitle, email, phone, makePrimary);
        _contacts.Add(contact);
        return contact;
    }

    public void UpdateContact(
        Guid contactId,
        string name,
        string? jobTitle,
        string? email,
        string? phone,
        bool isPrimary)
    {
        ClientContact contact = GetContact(contactId);
        bool makePrimary = isPrimary || _contacts.Count == 1;
        contact.Update(name, jobTitle, email, phone, isPrimary: false);
        if (makePrimary)
        {
            ClearPrimary();
            contact.SetPrimary(true);
        }
        else if (_contacts.All(existing => !existing.IsPrimary))
        {
            _contacts.FirstOrDefault()?.SetPrimary(true);
        }
    }

    public void RemoveContact(Guid contactId)
    {
        ClientContact contact = GetContact(contactId);
        _contacts.Remove(contact);
        if (contact.IsPrimary)
        {
            _contacts.FirstOrDefault()?.SetPrimary(true);
        }
    }

    public void ClearPrimaryContacts() => ClearPrimary();

    public void SetPrimaryContact(Guid contactId)
    {
        ClientContact contact = GetContact(contactId);
        ClearPrimary();
        contact.SetPrimary(true);
    }

    public void SetCreated(DateTimeOffset atUtc, Guid? byUserId)
    {
        CreatedAtUtc = atUtc;
        CreatedByUserId = byUserId;
    }

    public void SetUpdated(DateTimeOffset atUtc, Guid? byUserId)
    {
        UpdatedAtUtc = atUtc;
        UpdatedByUserId = byUserId;
    }

    private void ApplyProfile(
        ClientCode code,
        string name,
        string? email,
        string? phone,
        string? website,
        PostalAddress? billingAddress,
        string? notes)
    {
        Code = code.Value;
        Name = OrganizationText.Required(name, nameof(name), NameMaxLength);
        Email = OrganizationText.Optional(email, nameof(email), EmailMaxLength);
        Phone = OrganizationText.Optional(phone, nameof(phone), PhoneMaxLength);
        Website = NormalizeWebsite(website);
        BillingAddress = billingAddress;
        Notes = OrganizationText.Optional(notes, nameof(notes), NotesMaxLength);
    }

    private ClientContact GetContact(Guid contactId) =>
        _contacts.SingleOrDefault(contact => contact.Id == contactId)
        ?? throw new InvalidOperationException("The contact was not found for this client.");

    private void ClearPrimary()
    {
        foreach (ClientContact contact in _contacts)
        {
            if (contact.IsPrimary)
            {
                contact.SetPrimary(false);
            }
        }
    }

    private static string? NormalizeWebsite(string? website)
    {
        string? trimmed = OrganizationText.Optional(website, nameof(website), WebsiteMaxLength);
        if (trimmed is null)
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Website must be an http or https URL.", nameof(website));
        }

        return uri.AbsoluteUri;
    }
}

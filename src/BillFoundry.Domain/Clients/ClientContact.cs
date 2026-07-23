using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Clients;

/// <summary>
/// A person associated with a client. At most one contact per client may be primary.
/// </summary>
public sealed class ClientContact : IAuditable
{
    public const int NameMaxLength = 200;
    public const int JobTitleMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 50;

    private ClientContact()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ClientId { get; private set; }

    public string Name { get; private set; }

    public string? JobTitle { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    internal static ClientContact Create(
        Guid clientId,
        string name,
        string? jobTitle,
        string? email,
        string? phone,
        bool isPrimary)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(clientId, Guid.Empty);

        var contact = new ClientContact
        {
            Id = Guid.NewGuid(),
            ClientId = clientId
        };
        contact.Apply(name, jobTitle, email, phone, isPrimary);
        return contact;
    }

    internal void Update(string name, string? jobTitle, string? email, string? phone, bool isPrimary) =>
        Apply(name, jobTitle, email, phone, isPrimary);

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

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

    private void Apply(string name, string? jobTitle, string? email, string? phone, bool isPrimary)
    {
        Name = OrganizationText.Required(name, nameof(name), NameMaxLength);
        JobTitle = OrganizationText.Optional(jobTitle, nameof(jobTitle), JobTitleMaxLength);
        Email = OrganizationText.Optional(email, nameof(email), EmailMaxLength);
        Phone = OrganizationText.Optional(phone, nameof(phone), PhoneMaxLength);
        IsPrimary = isPrimary;
    }
}

using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Invoices;

/// <summary>
/// Client identity copied onto an invoice so later client edits do not rewrite
/// the billed-to name on a historical document.
/// </summary>
public sealed class InvoiceClientSnapshot
{
    private InvoiceClientSnapshot()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public string? Email { get; private set; }

    public static InvoiceClientSnapshot Capture(string name, string code, string? email)
    {
        return new InvoiceClientSnapshot
        {
            Name = OrganizationText.Required(name, nameof(name), Client.NameMaxLength),
            Code = OrganizationText.Required(code, nameof(code), ClientCode.MaxLength),
            Email = OrganizationText.Optional(email, nameof(email), Client.EmailMaxLength)
        };
    }

    public InvoiceClientSnapshot Clone() => Capture(Name, Code, Email);
}

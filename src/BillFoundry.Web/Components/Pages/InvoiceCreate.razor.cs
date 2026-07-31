using BillFoundry.Application.Invoices;
using BillFoundry.Web.Invoices;

namespace BillFoundry.Web.Components.Pages;

public partial class InvoiceCreate
{
    private bool _loading = true;

    private InvoiceInput Input { get; set; } = new();

    private IReadOnlyList<InvoiceClientOption> Clients { get; set; } = [];

    private string CurrencyCode { get; set; } = string.Empty;

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        InvoiceOptionsResult options = await Invoices.GetOptionsAsync();
        _loading = false;
        if (options.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (!options.Succeeded || options.Options is null)
        {
            ErrorMessage = options.Errors.Count > 0 ? options.Errors[0] : "Invoice options could not be loaded.";
            return;
        }

        Clients = options.Options.Clients;
        CurrencyCode = options.Options.CurrencyCode;
        Input.ApplyDefaults(options.Options);
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        Errors = [];
        InvoiceResult result = await Invoices.CreateAsync(Input.ToCreateCommand());
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.Succeeded && result.Invoice is not null)
        {
            Navigation.NavigateTo($"/Invoices/{result.Invoice.Id}");
            return;
        }

        Errors = [.. result.Errors];
        ErrorMessage = Errors.Count == 0 ? "The invoice could not be created." : null;
    }
}

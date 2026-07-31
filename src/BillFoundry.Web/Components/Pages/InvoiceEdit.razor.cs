using BillFoundry.Application.Invoices;
using BillFoundry.Web.Invoices;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class InvoiceEdit
{
    private bool _loading = true;
    private bool _notFound;
    private bool _canEdit = true;

    [Parameter]
    public Guid Id { get; set; }

    private InvoiceInput Input { get; set; } = new();

    private IReadOnlyList<InvoiceClientOption> Clients { get; set; } = [];

    private string? InvoiceNumber { get; set; }

    private string CurrencyCode { get; set; } = string.Empty;

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        InvoiceOptionsResult options = await Invoices.GetOptionsAsync();
        InvoiceResult result = await Invoices.GetAsync(Id);
        _loading = false;

        if (options.Succeeded && options.Options is not null)
        {
            Clients = options.Options.Clients;
            CurrencyCode = options.Options.CurrencyCode;
        }

        ApplyResult(result, successMessage: null);
    }

    private async Task SaveAsync()
    {
        ClearMessages();
        InvoiceResult result = await Invoices.UpdateHeaderAsync(Input.ToUpdateCommand(Id));
        ApplyResult(result, "Invoice details were saved.");
    }

    private void ApplyResult(InvoiceResult result, string? successMessage)
    {
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.IsNotFound)
        {
            _notFound = true;
            return;
        }

        if (result.Invoice is not null)
        {
            Input.CopyFrom(result.Invoice);
            InvoiceNumber = result.Invoice.Number;
            CurrencyCode = result.Invoice.CurrencyCode;
            _canEdit = result.Invoice.CanEdit;
            EnsureClientOption(result.Invoice);
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
            return;
        }

        Errors = [.. result.Errors];
        if (result.IsConcurrencyConflict)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The invoice was updated by another user.";
            Errors = [];
        }
    }

    private void EnsureClientOption(InvoiceDetailsDto invoice)
    {
        if (Clients.Any(client => client.Id == invoice.ClientId))
        {
            return;
        }

        Clients = [.. Clients, new InvoiceClientOption
        {
            Id = invoice.ClientId,
            Name = invoice.ClientIsActive ? invoice.ClientName : $"{invoice.ClientName} (inactive)",
            Code = invoice.ClientCode
        }];
    }

    private void ClearMessages()
    {
        StatusMessage = null;
        ErrorMessage = null;
        Errors = [];
    }
}

using BillFoundry.Application.Clients;
using BillFoundry.Web.Clients;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class ClientEdit
{
    private bool _loading = true;
    private bool _notFound;

    [Parameter]
    public Guid Id { get; set; }

    private ClientInput Input { get; set; } = new();

    private string? ClientName { get; set; }

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        ClientResult result = await Clients.GetAsync(Id);
        _loading = false;
        ApplyResult(result, successMessage: null, redirectOnSuccess: false);
    }

    private async Task SaveAsync()
    {
        ClearMessages();
        ClientResult result = await Clients.UpdateAsync(Input.ToUpdateCommand(Id));
        ApplyResult(result, "Client details were saved.", redirectOnSuccess: false);
    }

    private void ApplyResult(ClientResult result, string? successMessage, bool redirectOnSuccess)
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

        if (result.Client is not null)
        {
            Input.CopyFrom(result.Client);
            ClientName = result.Client.Name;
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
            if (redirectOnSuccess)
            {
                Navigation.NavigateTo($"/Clients/{Id}");
            }

            return;
        }

        Errors = [.. result.Errors];
        if (result.IsConcurrencyConflict)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The client was updated by another user.";
            Errors = [];
        }
    }

    private void ClearMessages()
    {
        StatusMessage = null;
        ErrorMessage = null;
        Errors = [];
    }
}

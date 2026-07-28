using BillFoundry.Application.Estimates;
using BillFoundry.Web.Estimates;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class EstimateEdit
{
    private bool _loading = true;
    private bool _notFound;
    private bool _canEdit = true;

    [Parameter]
    public Guid Id { get; set; }

    private EstimateInput Input { get; set; } = new();

    private IReadOnlyList<EstimateClientOption> Clients { get; set; } = [];

    private string? EstimateNumber { get; set; }

    private string CurrencyCode { get; set; } = string.Empty;

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        EstimateOptionsResult options = await Estimates.GetOptionsAsync();
        EstimateResult result = await Estimates.GetAsync(Id);
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
        EstimateResult result = await Estimates.UpdateHeaderAsync(Input.ToUpdateCommand(Id));
        ApplyResult(result, "Estimate details were saved.");
    }

    private void ApplyResult(EstimateResult result, string? successMessage)
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

        if (result.Estimate is not null)
        {
            Input.CopyFrom(result.Estimate);
            EstimateNumber = result.Estimate.Number;
            CurrencyCode = result.Estimate.CurrencyCode;
            _canEdit = result.Estimate.CanEdit;
            EnsureClientOption(result.Estimate);
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
            return;
        }

        Errors = [.. result.Errors];
        if (result.IsConcurrencyConflict)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The estimate was updated by another user.";
            Errors = [];
        }
    }

    private void EnsureClientOption(EstimateDetailsDto estimate)
    {
        if (Clients.Any(client => client.Id == estimate.ClientId))
        {
            return;
        }

        Clients = [.. Clients, new EstimateClientOption
        {
            Id = estimate.ClientId,
            Name = estimate.ClientIsActive ? estimate.ClientName : $"{estimate.ClientName} (inactive)",
            Code = "current"
        }];
    }

    private void ClearMessages()
    {
        StatusMessage = null;
        ErrorMessage = null;
        Errors = [];
    }
}

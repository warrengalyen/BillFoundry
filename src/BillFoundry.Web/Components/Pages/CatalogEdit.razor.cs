using BillFoundry.Application.Catalog;
using BillFoundry.Web.Catalog;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class CatalogEdit
{
    private bool _loading = true;
    private bool _notFound;

    [Parameter]
    public Guid Id { get; set; }

    private CatalogItemInput Input { get; set; } = new();

    private string? ItemName { get; set; }

    private string CurrencyCode { get; set; } = string.Empty;

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        CatalogItemResult result = await Catalog.GetAsync(Id);
        _loading = false;
        ApplyResult(result, successMessage: null);
    }

    private async Task SaveAsync()
    {
        ClearMessages();
        CatalogItemResult result = await Catalog.UpdateAsync(Input.ToUpdateCommand(Id));
        ApplyResult(result, "Catalog item details were saved.");
    }

    private void ApplyResult(CatalogItemResult result, string? successMessage)
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

        if (result.Item is not null)
        {
            Input.CopyFrom(result.Item);
            ItemName = result.Item.Name;
            CurrencyCode = result.Item.CurrencyCode;
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
            return;
        }

        Errors = [.. result.Errors];
        if (result.IsConcurrencyConflict)
        {
            ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The catalog item was updated by another user.";
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

using BillFoundry.Application.Catalog;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class CatalogDetail
{
    private bool _loading = true;
    private bool _notFound;

    [Parameter]
    public Guid Id { get; set; }

    private CatalogItemDetailsDto? Item { get; set; }

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        CatalogItemResult result = await Catalog.GetAsync(Id);
        _loading = false;
        ApplyResult(result, successMessage: null);
    }

    private async Task ActivateAsync()
    {
        if (Item is null)
        {
            return;
        }

        ClearMessages();
        CatalogItemResult result = await Catalog.ActivateAsync(new CatalogConcurrencyCommand
        {
            Id = Id,
            RowVersion = Item.RowVersion
        });
        ApplyResult(result, "The catalog item is active.");
    }

    private async Task DeactivateAsync()
    {
        if (Item is null)
        {
            return;
        }

        ClearMessages();
        CatalogItemResult result = await Catalog.DeactivateAsync(new CatalogConcurrencyCommand
        {
            Id = Id,
            RowVersion = Item.RowVersion
        });
        ApplyResult(result, "The catalog item is inactive. Existing records can still reference this item.");
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
            Item = null;
            return;
        }

        if (result.Item is not null)
        {
            Item = result.Item;
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
            return;
        }

        ErrorMessage = result.Errors.Count > 0 ? result.Errors[0] : "The catalog item could not be updated.";
    }

    private void ClearMessages()
    {
        StatusMessage = null;
        ErrorMessage = null;
    }
}

using BillFoundry.Application.Catalog;
using BillFoundry.Web.Catalog;

namespace BillFoundry.Web.Components.Pages;

public partial class CatalogCreate
{
    private CatalogItemInput Input { get; set; } = new();

    private string CurrencyCode { get; set; } = string.Empty;

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        CatalogListResult list = await Catalog.ListAsync(new CatalogListQuery { PageSize = 1, Status = CatalogStatusFilter.All });
        if (list.Succeeded)
        {
            CurrencyCode = list.CurrencyCode;
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        Errors = [];
        CatalogItemResult result = await Catalog.CreateAsync(Input.ToCreateCommand());
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.Succeeded && result.Item is not null)
        {
            Navigation.NavigateTo($"/Catalog/{result.Item.Id}");
            return;
        }

        Errors = [.. result.Errors];
        ErrorMessage = Errors.Count == 0 ? "The catalog item could not be created." : null;
    }
}

using BillFoundry.Application.Estimates;
using BillFoundry.Web.Estimates;

namespace BillFoundry.Web.Components.Pages;

public partial class EstimateCreate
{
    private bool _loading = true;

    private EstimateInput Input { get; set; } = new();

    private IReadOnlyList<EstimateClientOption> Clients { get; set; } = [];

    private string CurrencyCode { get; set; } = string.Empty;

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        EstimateOptionsResult options = await Estimates.GetOptionsAsync();
        _loading = false;
        if (options.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (!options.Succeeded || options.Options is null)
        {
            ErrorMessage = options.Errors.Count > 0 ? options.Errors[0] : "Estimate options could not be loaded.";
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
        EstimateResult result = await Estimates.CreateAsync(Input.ToCreateCommand());
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.Succeeded && result.Estimate is not null)
        {
            Navigation.NavigateTo($"/Estimates/{result.Estimate.Id}");
            return;
        }

        Errors = [.. result.Errors];
        ErrorMessage = Errors.Count == 0 ? "The estimate could not be created." : null;
    }
}

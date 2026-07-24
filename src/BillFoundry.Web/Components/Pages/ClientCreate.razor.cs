using BillFoundry.Application.Clients;
using BillFoundry.Web.Clients;

namespace BillFoundry.Web.Components.Pages;

public partial class ClientCreate
{
    private ClientInput Input { get; set; } = new();

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        Errors = [];
        ClientResult result = await Clients.CreateAsync(Input.ToCreateCommand());
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.Succeeded && result.Client is not null)
        {
            Navigation.NavigateTo($"/Clients/{result.Client.Id}");
            return;
        }

        Errors = [.. result.Errors];
        ErrorMessage = Errors.Count == 0 ? "The client could not be created." : null;
    }
}

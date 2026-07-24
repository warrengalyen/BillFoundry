using System.ComponentModel.DataAnnotations;
using BillFoundry.Application.Clients;
using BillFoundry.Domain.Clients;
using Microsoft.AspNetCore.Components;

namespace BillFoundry.Web.Components.Pages;

public partial class ClientDetail
{
    private bool _loading = true;
    private bool _notFound;
    private Guid? _editingContactId;

    [Parameter]
    public Guid Id { get; set; }

    private ClientDetailsDto? Client { get; set; }

    private ContactInputModel ContactInput { get; set; } = new();

    private string? StatusMessage { get; set; }

    private string? ErrorMessage { get; set; }

    private List<string> Errors { get; set; } = [];

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        ClientResult result = await Clients.GetAsync(Id);
        _loading = false;
        ApplyResult(result, successMessage: null);
    }

    private async Task ActivateAsync()
    {
        if (Client is null)
        {
            return;
        }

        ClearMessages();
        ClientResult result = await Clients.ActivateAsync(new ClientConcurrencyCommand
        {
            Id = Id,
            RowVersion = Client.RowVersion
        });
        ApplyResult(result, "The client is active.");
    }

    private async Task DeactivateAsync()
    {
        if (Client is null)
        {
            return;
        }

        ClearMessages();
        ClientResult result = await Clients.DeactivateAsync(new ClientConcurrencyCommand
        {
            Id = Id,
            RowVersion = Client.RowVersion
        });
        ApplyResult(result, "The client is inactive. Existing records can still reference this client.");
    }

    private void BeginEditContact(ClientContactDto contact)
    {
        _editingContactId = contact.Id;
        ContactInput = new ContactInputModel
        {
            Name = contact.Name,
            JobTitle = contact.JobTitle,
            Email = contact.Email,
            Phone = contact.Phone,
            IsPrimary = contact.IsPrimary
        };
        ClearMessages();
    }

    private void CancelContactEdit()
    {
        _editingContactId = null;
        ContactInput = new();
    }

    private async Task SaveContactAsync()
    {
        if (Client is null)
        {
            return;
        }

        ClearMessages();
        ClientResult result;
        if (_editingContactId is Guid contactId)
        {
            result = await Clients.UpdateContactAsync(new UpdateContactCommand
            {
                ClientId = Id,
                ContactId = contactId,
                RowVersion = Client.RowVersion,
                Name = ContactInput.Name,
                JobTitle = ContactInput.JobTitle,
                Email = ContactInput.Email,
                Phone = ContactInput.Phone,
                IsPrimary = ContactInput.IsPrimary
            });
        }
        else
        {
            result = await Clients.AddContactAsync(new SaveContactCommand
            {
                ClientId = Id,
                RowVersion = Client.RowVersion,
                Name = ContactInput.Name,
                JobTitle = ContactInput.JobTitle,
                Email = ContactInput.Email,
                Phone = ContactInput.Phone,
                IsPrimary = ContactInput.IsPrimary
            });
        }

        string message = _editingContactId is null ? "The contact was added." : "The contact was saved.";
        if (result.Succeeded)
        {
            CancelContactEdit();
            ApplyResult(result, message);
            return;
        }

        ApplyResult(result, successMessage: null);
    }

    private async Task RemoveContactAsync(Guid contactId)
    {
        if (Client is null)
        {
            return;
        }

        ClearMessages();
        ClientResult result = await Clients.RemoveContactAsync(new RemoveContactCommand
        {
            Id = Id,
            ContactId = contactId,
            RowVersion = Client.RowVersion
        });
        ApplyResult(result, "The contact was removed.");
    }

    private async Task SetPrimaryAsync(Guid contactId)
    {
        if (Client is null)
        {
            return;
        }

        ClearMessages();
        ClientResult result = await Clients.SetPrimaryContactAsync(new SetPrimaryContactCommand
        {
            Id = Id,
            ContactId = contactId,
            RowVersion = Client.RowVersion
        });
        ApplyResult(result, "The primary contact was updated.");
    }

    private void ApplyResult(ClientResult result, string? successMessage)
    {
        if (result.IsForbidden)
        {
            Navigation.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (result.IsNotFound)
        {
            _notFound = true;
            Client = null;
            return;
        }

        if (result.Client is not null)
        {
            Client = result.Client;
        }

        if (result.Succeeded)
        {
            StatusMessage = successMessage;
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

    private static string FormatAddress(ClientDetailsDto client)
    {
        var parts = new[]
        {
            client.AddressLine1,
            client.AddressLine2,
            string.Join(" ", new[] { client.City, client.Region, client.PostalCode }.Where(value => !string.IsNullOrWhiteSpace(value))),
            client.Country
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        string formatted = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(formatted) ? "—" : formatted;
    }

    private sealed class ContactInputModel
    {
        [Required]
        [StringLength(ClientContact.NameMaxLength)]
        public string Name { get; set; } = string.Empty;

        [StringLength(ClientContact.JobTitleMaxLength)]
        public string? JobTitle { get; set; }

        [EmailAddress]
        [StringLength(ClientContact.EmailMaxLength)]
        public string? Email { get; set; }

        [StringLength(ClientContact.PhoneMaxLength)]
        public string? Phone { get; set; }

        public bool IsPrimary { get; set; }
    }
}

namespace BillFoundry.Application.Clients;

/// <summary>
/// Creates, updates, and lists clients for the installation. Mutations require
/// the <c>ManageClients</c> policy. Clients are deactivated rather than deleted.
/// </summary>
public interface IClientService
{
    Task<ClientListResult> ListAsync(ClientListQuery query, CancellationToken cancellationToken = default);

    Task<ClientResult> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ClientResult> CreateAsync(SaveClientCommand command, CancellationToken cancellationToken = default);

    Task<ClientResult> UpdateAsync(UpdateClientCommand command, CancellationToken cancellationToken = default);

    Task<ClientResult> ActivateAsync(ClientConcurrencyCommand command, CancellationToken cancellationToken = default);

    Task<ClientResult> DeactivateAsync(ClientConcurrencyCommand command, CancellationToken cancellationToken = default);

    Task<ClientResult> AddContactAsync(SaveContactCommand command, CancellationToken cancellationToken = default);

    Task<ClientResult> UpdateContactAsync(UpdateContactCommand command, CancellationToken cancellationToken = default);

    Task<ClientResult> RemoveContactAsync(RemoveContactCommand command, CancellationToken cancellationToken = default);

    Task<ClientResult> SetPrimaryContactAsync(SetPrimaryContactCommand command, CancellationToken cancellationToken = default);
}

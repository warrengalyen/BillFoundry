using BillFoundry.Application.Clients;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Clients;

internal sealed class ClientService(
    BillFoundryDbContext dbContext,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IClientService
{
    public async Task<ClientListResult> ListAsync(ClientListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientListResult.Forbidden();
        }

        query.Normalize();
        IQueryable<Client> clients = dbContext.Clients.AsNoTracking();

        clients = query.Status switch
        {
            ClientStatusFilter.Active => clients.Where(client => client.IsActive),
            ClientStatusFilter.Inactive => clients.Where(client => !client.IsActive),
            _ => clients
        };

        if (query.Search is not null)
        {
            string search = query.Search;
            clients = clients.Where(client =>
                client.Name.Contains(search)
                || client.Code.Contains(search)
                || (client.Email != null && client.Email.Contains(search))
                || (client.Phone != null && client.Phone.Contains(search))
                || client.Contacts.Any(contact => contact.Name.Contains(search)));
        }

        int totalCount = await clients.CountAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<Client> sorted = query.SortBy switch
        {
            ClientSortField.Code when query.SortDescending => clients.OrderByDescending(client => client.Code).ThenBy(client => client.Name),
            ClientSortField.Code => clients.OrderBy(client => client.Code).ThenBy(client => client.Name),
            ClientSortField.Email when query.SortDescending => clients.OrderByDescending(client => client.Email).ThenBy(client => client.Name),
            ClientSortField.Email => clients.OrderBy(client => client.Email).ThenBy(client => client.Name),
            ClientSortField.CreatedAt when query.SortDescending => clients.OrderByDescending(client => client.CreatedAtUtc).ThenBy(client => client.Name),
            ClientSortField.CreatedAt => clients.OrderBy(client => client.CreatedAtUtc).ThenBy(client => client.Name),
            _ when query.SortDescending => clients.OrderByDescending(client => client.Name).ThenBy(client => client.Code),
            _ => clients.OrderBy(client => client.Name).ThenBy(client => client.Code)
        };

        List<ClientListItemDto> items = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(client => new ClientListItemDto
            {
                Id = client.Id,
                Code = client.Code,
                Name = client.Name,
                Email = client.Email,
                Phone = client.Phone,
                IsActive = client.IsActive,
                PrimaryContactName = client.Contacts
                    .Where(contact => contact.IsPrimary)
                    .Select(contact => contact.Name)
                    .FirstOrDefault(),
                CreatedAtUtc = client.CreatedAtUtc
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ClientListResult.Success(new PagedResult<ClientListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    public async Task<ClientResult> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientResult.Forbidden();
        }

        Client? client = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        return client is null ? ClientResult.NotFound() : ClientResult.Success(ToDetails(client));
    }

    public async Task<ClientResult> CreateAsync(SaveClientCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientResult.Forbidden();
        }

        IReadOnlyList<string> errors = ClientValidator.Validate(command, requireRowVersion: false);
        if (errors.Count > 0)
        {
            return ClientResult.Invalid(errors);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        int number = await AllocateNumberAsync(cancellationToken).ConfigureAwait(false);
        ClientCode code = string.IsNullOrWhiteSpace(command.Code)
            ? ClientCode.FromNumber(number)
            : ClientCode.Parse(command.Code);

        if (await dbContext.Clients.AnyAsync(client => client.Code == code.Value, cancellationToken).ConfigureAwait(false))
        {
            return ClientResult.Invalid(["A client with this code already exists."]);
        }

        var client = Client.Create(
            number,
            code,
            command.Name,
            command.Email,
            command.Phone,
            command.Website,
            CreateAddress(command),
            command.Notes);

        dbContext.Clients.Add(client);
        ClientResult result = await SaveAsync(client, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<ClientResult> UpdateAsync(UpdateClientCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientResult.Forbidden();
        }

        Client? client = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return ClientResult.NotFound();
        }

        IReadOnlyList<string> errors = ClientValidator.Validate(command, requireRowVersion: true);
        if (errors.Count > 0)
        {
            return ClientResult.Invalid(errors, ToDetails(client));
        }

        ClientCode code = string.IsNullOrWhiteSpace(command.Code)
            ? ClientCode.Parse(client.Code)
            : ClientCode.Parse(command.Code);

        if (await dbContext.Clients.AnyAsync(
                existing => existing.Id != client.Id && existing.Code == code.Value,
                cancellationToken).ConfigureAwait(false))
        {
            return ClientResult.Invalid(["A client with this code already exists."], ToDetails(client));
        }

        ApplyRowVersion(client, command.RowVersion);
        client.Update(code, command.Name, command.Email, command.Phone, command.Website, CreateAddress(command), command.Notes);
        return await SaveAsync(client, cancellationToken).ConfigureAwait(false);
    }

    public Task<ClientResult> ActivateAsync(ClientConcurrencyCommand command, CancellationToken cancellationToken = default) =>
        SetActiveAsync(command, active: true, cancellationToken);

    public Task<ClientResult> DeactivateAsync(ClientConcurrencyCommand command, CancellationToken cancellationToken = default) =>
        SetActiveAsync(command, active: false, cancellationToken);

    public async Task<ClientResult> AddContactAsync(SaveContactCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientResult.Forbidden();
        }

        Client? client = await LoadAsync(command.ClientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return ClientResult.NotFound();
        }

        IReadOnlyList<string> errors = ClientValidator.Validate(command);
        if (errors.Count > 0)
        {
            return ClientResult.Invalid(errors, ToDetails(client));
        }

        ApplyRowVersion(client, command.RowVersion);
        bool willBePrimary = command.IsPrimary || client.Contacts.Count == 0;
        return await MutateContactsAsync(
            client,
            willBePrimary,
            () => client.AddContact(command.Name, command.JobTitle, command.Email, command.Phone, command.IsPrimary),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientResult> UpdateContactAsync(UpdateContactCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientResult.Forbidden();
        }

        Client? client = await LoadAsync(command.ClientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return ClientResult.NotFound();
        }

        IReadOnlyList<string> errors = ClientValidator.Validate(command);
        if (errors.Count > 0)
        {
            return ClientResult.Invalid(errors, ToDetails(client));
        }

        ApplyRowVersion(client, command.RowVersion);
        bool willBePrimary = command.IsPrimary || client.Contacts.Count == 1;
        return await MutateContactsAsync(
            client,
            willBePrimary,
            () => client.UpdateContact(command.ContactId, command.Name, command.JobTitle, command.Email, command.Phone, command.IsPrimary),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientResult> RemoveContactAsync(RemoveContactCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientResult.Forbidden();
        }

        Client? client = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return ClientResult.NotFound();
        }

        ApplyRowVersion(client, command.RowVersion);
        try
        {
            client.RemoveContact(command.ContactId);
        }
        catch (InvalidOperationException)
        {
            return ClientResult.Invalid(["The contact was not found for this client."], ToDetails(client));
        }

        Touch(client);
        return await SaveAsync(client, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientResult> SetPrimaryContactAsync(SetPrimaryContactCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientResult.Forbidden();
        }

        Client? client = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return ClientResult.NotFound();
        }

        ApplyRowVersion(client, command.RowVersion);
        return await MutateContactsAsync(
            client,
            willBePrimary: true,
            () => client.SetPrimaryContact(command.ContactId),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ClientResult> SetActiveAsync(
        ClientConcurrencyCommand command,
        bool active,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return ClientResult.Forbidden();
        }

        Client? client = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return ClientResult.NotFound();
        }

        if (command.RowVersion is not { Length: > 0 })
        {
            return ClientResult.Invalid(
                ["The client version is missing. Reload the page and try again."],
                ToDetails(client));
        }

        ApplyRowVersion(client, command.RowVersion);
        if (active)
        {
            client.Activate();
        }
        else
        {
            client.Deactivate();
        }

        return await SaveAsync(client, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsForbiddenAsync()
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.ManageClients)
            .ConfigureAwait(false);
        return !authorization.Succeeded;
    }

    private Task<Client?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Clients
            .Include(client => client.Contacts)
            .FirstOrDefaultAsync(client => client.Id == id, cancellationToken);

    private async Task<ClientResult> MutateContactsAsync(
        Client client,
        bool willBePrimary,
        Action mutate,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (willBePrimary && client.PrimaryContact is not null)
            {
                client.ClearPrimaryContacts();
                Touch(client);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            mutate();
            Touch(client);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ClientResult.Success(ToDetails(client));
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ClientResult.Invalid(
                ["The contact was not found for this client."],
                await ReloadDetailsAsync(client, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ClientResult.ConcurrencyConflict(await ReloadDetailsAsync(client, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ClientResult.Invalid(
                ["A client with this code already exists, or another contact is already marked primary."],
                await ReloadDetailsAsync(client, cancellationToken).ConfigureAwait(false));
        }
    }

    private async Task<int> AllocateNumberAsync(CancellationToken cancellationToken)
    {
        int? max = await dbContext.Clients.MaxAsync(client => (int?)client.Number, cancellationToken).ConfigureAwait(false);
        return (max ?? 0) + 1;
    }

    private void ApplyRowVersion(Client client, byte[] rowVersion)
    {
        dbContext.Entry(client).Property(entity => entity.RowVersion).OriginalValue = rowVersion;
    }

    private void Touch(Client client)
    {
        client.SetUpdated(timeProvider.GetUtcNow(), currentUser.UserId);
        dbContext.Entry(client).Property(entity => entity.UpdatedAtUtc).IsModified = true;
        dbContext.Entry(client).Property(entity => entity.UpdatedByUserId).IsModified = true;
    }

    private ClientDetailsDto ToDetails(Client client)
    {
        byte[] rowVersion = dbContext.Entry(client).Property(entity => entity.RowVersion).CurrentValue
            ?? client.RowVersion;
        return ClientDetailsDto.From(client, rowVersion);
    }

    private async Task<ClientDetailsDto> ReloadDetailsAsync(Client client, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Client current = await LoadAsync(client.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The client could not be reloaded.");
        return ToDetails(current);
    }

    private async Task<ClientResult> SaveAsync(Client client, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ClientResult.Success(ToDetails(client));
        }
        catch (DbUpdateConcurrencyException)
        {
            return ClientResult.ConcurrencyConflict(await ReloadDetailsAsync(client, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return ClientResult.Invalid(
                ["A client with this code already exists, or another contact is already marked primary."],
                ToDetails(client));
        }
    }

    private static PostalAddress? CreateAddress(SaveClientCommand command)
    {
        bool anyAddress = !string.IsNullOrWhiteSpace(command.AddressLine1)
            || !string.IsNullOrWhiteSpace(command.AddressLine2)
            || !string.IsNullOrWhiteSpace(command.City)
            || !string.IsNullOrWhiteSpace(command.Region)
            || !string.IsNullOrWhiteSpace(command.PostalCode)
            || !string.IsNullOrWhiteSpace(command.Country);

        if (!anyAddress)
        {
            return null;
        }

        return PostalAddress.Create(
            command.AddressLine1,
            command.AddressLine2,
            command.City,
            command.Region,
            command.PostalCode,
            command.Country);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Number is 2601 or 2627;
}

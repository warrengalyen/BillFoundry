using BillFoundry.Application.Clients;
using BillFoundry.Application.Security;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ClientPersistenceTests
{
    private readonly SqlServerFixture _sql;
    private readonly string _marker;

    public ClientPersistenceTests(SqlServerFixture sql)
    {
        _sql = sql;
        _marker = $"Probe-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task Create_assigns_code_and_persists_profile()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser());
        IClientService service = provider.GetRequiredService<IClientService>();

        ClientResult created = await service.CreateAsync(new SaveClientCommand
        {
            Name = $"{_marker} Acme",
            Email = "billing@acme.test",
            Phone = "555-0100",
            Website = "https://acme.test",
            AddressLine1 = "10 Main St",
            City = "Springfield",
            Region = "IL",
            PostalCode = "62701",
            Country = "United States"
        });

        Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        Assert.False(string.IsNullOrWhiteSpace(created.Client?.Code));
        Assert.True(created.Client?.IsActive);
        Assert.Equal("10 Main St", created.Client?.AddressLine1);

        ClientResult reloaded = await service.GetAsync(created.Client!.Id);
        Assert.Equal($"{_marker} Acme", reloaded.Client?.Name);
        Assert.Equal(created.Client.Code, reloaded.Client?.Code);
    }

    [Fact]
    public async Task List_pages_server_side_without_returning_the_full_set()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();
        for (int index = 1; index <= 25; index++)
        {
            ClientResult created = await service.CreateAsync(new SaveClientCommand
            {
                Name = $"{_marker} Client {index:D2}",
                Email = $"c{index}@probe.test"
            });
            Assert.True(created.Succeeded, string.Join("; ", created.Errors));
        }

        ClientListResult page = await service.ListAsync(new ClientListQuery
        {
            Search = _marker,
            Status = ClientStatusFilter.All,
            Page = 2,
            PageSize = 10,
            SortBy = ClientSortField.Name
        });

        Assert.True(page.Succeeded);
        Assert.Equal(25, page.Page?.TotalCount);
        Assert.Equal(10, page.Page?.Items.Count);
        Assert.Equal(2, page.Page?.Page);
        Assert.DoesNotContain(page.Page!.Items, item => item.Name.EndsWith("01", StringComparison.Ordinal));
    }

    [Fact]
    public async Task List_filters_inactive_clients_out_of_the_active_view()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();
        ClientResult created = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} Inactive Co" });
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));

        ClientResult deactivated = await service.DeactivateAsync(new ClientConcurrencyCommand
        {
            Id = created.Client!.Id,
            RowVersion = created.Client.RowVersion
        });
        Assert.True(deactivated.Succeeded, string.Join("; ", deactivated.Errors));

        ClientListResult active = await service.ListAsync(new ClientListQuery
        {
            Search = _marker,
            Status = ClientStatusFilter.Active
        });
        ClientListResult inactive = await service.ListAsync(new ClientListQuery
        {
            Search = _marker,
            Status = ClientStatusFilter.Inactive
        });

        Assert.DoesNotContain(active.Page!.Items, item => item.Id == created.Client.Id);
        Assert.Contains(inactive.Page!.Items, item => item.Id == created.Client.Id && !item.IsActive);
    }

    [Fact]
    public async Task Duplicate_client_code_is_rejected()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();
        string code = $"X{_marker[..8]}";

        ClientResult first = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} One", Code = code });
        ClientResult second = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} Two", Code = code });

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));
        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, error => error.Contains("code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_detects_stale_row_version()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();
        ClientResult created = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} Concurrent" });
        byte[] stale = created.Client!.RowVersion;

        ClientResult first = await service.UpdateAsync(new UpdateClientCommand
        {
            Id = created.Client.Id,
            RowVersion = stale,
            Name = $"{_marker} First"
        });
        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        ClientResult second = await service.UpdateAsync(new UpdateClientCommand
        {
            Id = created.Client.Id,
            RowVersion = stale,
            Name = $"{_marker} Second"
        });

        Assert.True(second.IsConcurrencyConflict);
        Assert.Equal($"{_marker} First", second.Client?.Name);
    }

    [Fact]
    public async Task Update_can_chain_using_the_returned_row_version()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();
        ClientResult created = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} Chain" });
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));

        ClientResult first = await service.UpdateAsync(new UpdateClientCommand
        {
            Id = created.Client!.Id,
            RowVersion = created.Client.RowVersion,
            Name = $"{_marker} Once"
        });
        Assert.True(first.Succeeded, string.Join("; ", first.Errors));

        ClientResult second = await service.UpdateAsync(new UpdateClientCommand
        {
            Id = created.Client.Id,
            RowVersion = first.Client!.RowVersion,
            Name = $"{_marker} Twice"
        });

        Assert.True(second.Succeeded, string.Join("; ", second.Errors));
        Assert.Equal($"{_marker} Twice", second.Client?.Name);
    }

    [Fact]
    public async Task Only_one_contact_can_be_primary_for_a_client()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();
        ClientResult created = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} Contacts" });

        ClientResult first = await service.AddContactAsync(new SaveContactCommand
        {
            ClientId = created.Client!.Id,
            RowVersion = created.Client.RowVersion,
            Name = "Ada",
            IsPrimary = true
        });
        ClientResult second = await service.AddContactAsync(new SaveContactCommand
        {
            ClientId = created.Client.Id,
            RowVersion = first.Client!.RowVersion,
            Name = "Ben",
            IsPrimary = true
        });

        Assert.True(second.Succeeded, string.Join("; ", second.Errors));
        Assert.Equal(1, second.Client!.Contacts.Count(contact => contact.IsPrimary));
        Assert.Equal("Ben", second.Client.Contacts.Single(contact => contact.IsPrimary).Name);
    }

    [Fact]
    public async Task Set_primary_contact_clears_the_previous_primary()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();
        ClientResult created = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} Primary" });

        ClientResult ada = await service.AddContactAsync(new SaveContactCommand
        {
            ClientId = created.Client!.Id,
            RowVersion = created.Client.RowVersion,
            Name = "Ada",
            IsPrimary = true
        });
        ClientResult ben = await service.AddContactAsync(new SaveContactCommand
        {
            ClientId = created.Client.Id,
            RowVersion = ada.Client!.RowVersion,
            Name = "Ben",
            IsPrimary = false
        });
        Assert.True(ben.Succeeded, string.Join("; ", ben.Errors));

        Guid benId = ben.Client!.Contacts.Single(contact => contact.Name == "Ben").Id;
        ClientResult primary = await service.SetPrimaryContactAsync(new SetPrimaryContactCommand
        {
            Id = created.Client.Id,
            ContactId = benId,
            RowVersion = ben.Client.RowVersion
        });

        Assert.True(primary.Succeeded, string.Join("; ", primary.Errors));
        Assert.Equal("Ben", primary.Client!.Contacts.Single(contact => contact.IsPrimary).Name);
        Assert.Equal(1, primary.Client.Contacts.Count(contact => contact.IsPrimary));
    }

    [Fact]
    public async Task Database_rejects_two_primary_contacts_for_the_same_client()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();
        ClientResult created = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} Constraint" });
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));

        await using var db = new BillFoundryDbContext(new DbContextOptionsBuilder<BillFoundryDbContext>()
            .UseSqlServer(_sql.ConnectionString)
            .Options);

        Guid clientId = created.Client!.Id;
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ClientContacts (Id, ClientId, Name, IsPrimary, CreatedAtUtc)
VALUES ({Guid.NewGuid()}, {clientId}, N'Ada', 1, {createdAt})");

        SqlException exception = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ClientContacts (Id, ClientId, Name, IsPrimary, CreatedAtUtc)
VALUES ({Guid.NewGuid()}, {clientId}, N'Ben', 1, {createdAt})");
        });

        Assert.True(exception.Number is 2601 or 2627);
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_manage_clients()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        IClientService service = provider.GetRequiredService<IClientService>();

        ClientListResult list = await service.ListAsync(new ClientListQuery());
        ClientResult create = await service.CreateAsync(new SaveClientCommand { Name = $"{_marker} Denied" });

        Assert.True(list.IsForbidden);
        Assert.True(create.IsForbidden);
    }

    [Fact]
    public async Task Validation_errors_do_not_persist_a_client()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IClientService service = provider.GetRequiredService<IClientService>();

        ClientResult result = await service.CreateAsync(new SaveClientCommand
        {
            Name = " ",
            Email = "bad"
        });

        Assert.False(result.Succeeded);
        ClientListResult list = await service.ListAsync(new ClientListQuery
        {
            Search = _marker,
            Status = ClientStatusFilter.All
        });
        Assert.Empty(list.Page!.Items);
    }
}

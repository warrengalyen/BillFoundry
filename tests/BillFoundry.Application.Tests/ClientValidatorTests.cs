using BillFoundry.Application.Clients;

namespace BillFoundry.Application.Tests;

public sealed class ClientValidatorTests
{
    [Fact]
    public void Validate_accepts_a_minimal_client()
    {
        IReadOnlyList<string> errors = ClientValidator.Validate(
            new SaveClientCommand { Name = "Acme" },
            requireRowVersion: false);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_requires_name()
    {
        IReadOnlyList<string> errors = ClientValidator.Validate(
            new SaveClientCommand { Name = " " },
            requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("Name", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rejects_invalid_email_website_and_code()
    {
        var command = new SaveClientCommand
        {
            Code = "bad code",
            Name = "Acme",
            Email = "not-an-email",
            Website = "javascript:alert(1)"
        };

        IReadOnlyList<string> errors = ClientValidator.Validate(command, requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("Client code", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Billing email", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Website", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_requires_core_address_fields_when_any_address_is_present()
    {
        var command = new SaveClientCommand
        {
            Name = "Acme",
            City = "Springfield"
        };

        IReadOnlyList<string> errors = ClientValidator.Validate(command, requireRowVersion: false);

        Assert.Contains(errors, error => error.Contains("Address line 1", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Country", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_contact_requires_name_and_row_version()
    {
        IReadOnlyList<string> errors = ClientValidator.Validate(new SaveContactCommand
        {
            Name = " ",
            Email = "bad"
        });

        Assert.Contains(errors, error => error.Contains("Contact name", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Email", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("version", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class ClientListQueryTests
{
    [Fact]
    public void Normalize_clamps_page_and_page_size()
    {
        var query = new ClientListQuery
        {
            Search = "  acme  ",
            Page = 0,
            PageSize = 500,
            SortBy = (ClientSortField)42,
            Status = (ClientStatusFilter)9
        };

        query.Normalize();

        Assert.Equal("acme", query.Search);
        Assert.Equal(1, query.Page);
        Assert.Equal(ClientListQuery.MaxPageSize, query.PageSize);
        Assert.Equal(ClientSortField.Name, query.SortBy);
        Assert.Equal(ClientStatusFilter.Active, query.Status);
    }
}

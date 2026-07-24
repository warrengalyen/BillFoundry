using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Organizations;

namespace BillFoundry.Domain.Tests;

public sealed class ClientCodeTests
{
    [Theory]
    [InlineData("c0001", "C0001")]
    [InlineData("Acme-1", "ACME-1")]
    [InlineData("A.B_1", "A.B_1")]
    public void TryCreate_normalizes_supported_codes(string input, string expected)
    {
        Assert.True(ClientCode.TryCreate(input, out ClientCode code));
        Assert.Equal(expected, code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-ABC")]
    [InlineData("A code")]
    [InlineData("THIS-CODE-IS-WAY-TOO-LONG")]
    public void TryCreate_rejects_invalid_codes(string? input)
    {
        Assert.False(ClientCode.TryCreate(input, out _));
    }

    [Fact]
    public void FromNumber_pads_to_four_digits()
    {
        Assert.Equal("C0007", ClientCode.FromNumber(7).Value);
    }
}

public sealed class ClientTests
{
    [Fact]
    public void Create_starts_active_without_contacts()
    {
        Client client = CreateClient();

        Assert.True(client.IsActive);
        Assert.Empty(client.Contacts);
        Assert.Equal("C0001", client.Code);
        Assert.Null(client.PrimaryContact);
    }

    [Fact]
    public void Deactivate_and_activate_toggle_status()
    {
        Client client = CreateClient();

        client.Deactivate();
        Assert.False(client.IsActive);

        client.Activate();
        Assert.True(client.IsActive);
    }

    [Fact]
    public void First_contact_becomes_primary()
    {
        Client client = CreateClient();

        ClientContact contact = client.AddContact("Ada", "Owner", "ada@client.test", null, isPrimary: false);

        Assert.True(contact.IsPrimary);
        Assert.Same(contact, client.PrimaryContact);
    }

    [Fact]
    public void Adding_a_primary_contact_clears_the_previous_primary()
    {
        Client client = CreateClient();
        ClientContact first = client.AddContact("Ada", null, null, null, isPrimary: true);
        ClientContact second = client.AddContact("Ben", null, null, null, isPrimary: true);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Equal(second.Id, client.PrimaryContact?.Id);
        Assert.Equal(1, client.Contacts.Count(contact => contact.IsPrimary));
    }

    [Fact]
    public void SetPrimaryContact_enforces_a_single_primary()
    {
        Client client = CreateClient();
        ClientContact first = client.AddContact("Ada", null, null, null, isPrimary: true);
        ClientContact second = client.AddContact("Ben", null, null, null, isPrimary: false);

        client.SetPrimaryContact(second.Id);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
    }

    [Fact]
    public void Removing_primary_promotes_remaining_contact()
    {
        Client client = CreateClient();
        ClientContact first = client.AddContact("Ada", null, null, null, isPrimary: true);
        client.AddContact("Ben", null, null, null, isPrimary: false);

        client.RemoveContact(first.Id);

        Assert.Single(client.Contacts);
        Assert.True(client.Contacts[0].IsPrimary);
        Assert.Equal("Ben", client.PrimaryContact?.Name);
    }

    [Fact]
    public void Update_replaces_profile_fields()
    {
        Client client = CreateClient();
        PostalAddress address = PostalAddress.Create("10 Main", null, "Springfield", "IL", "62701", "United States");

        client.Update(
            ClientCode.Parse("ACME"),
            "Acme LLC",
            "billing@acme.test",
            "555-0100",
            "https://acme.test",
            address,
            "Net 15");

        Assert.Equal("ACME", client.Code);
        Assert.Equal("Acme LLC", client.Name);
        Assert.Equal("https://acme.test/", client.Website);
        Assert.Equal("10 Main", client.BillingAddress?.Line1);
    }

    private static Client CreateClient() =>
        Client.Create(
            1,
            ClientCode.FromNumber(1),
            "Acme",
            "billing@acme.test",
            null,
            null,
            null,
            null);
}

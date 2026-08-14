using BillFoundry.Application.Configuration;

namespace BillFoundry.Application.Tests;

public sealed class DemoSeedOptionsTests
{
    [Fact]
    public void Defaults_use_fictional_north_beacon_accounts_and_are_disabled()
    {
        var options = new DemoSeedOptions();

        Assert.False(options.Enabled);
        Assert.False(options.ResetOnStartup);
        Assert.Equal("admin@northbeacon.example", options.AdministratorEmail);
        Assert.Equal("Demo-Admin-Passw0rd!", options.AdministratorPassword);
        Assert.Equal("user@northbeacon.example", options.UserEmail);
        Assert.Equal("Demo-User-Passw0rd!", options.UserPassword);
    }
}

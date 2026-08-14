using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BillFoundry.IntegrationTests;

public class BillFoundryWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("IdentitySeed:Enabled", "false");
        builder.UseSetting("DemoMode:Enabled", "false");
        builder.UseSetting("DemoSeed:Enabled", "false");
    }
}

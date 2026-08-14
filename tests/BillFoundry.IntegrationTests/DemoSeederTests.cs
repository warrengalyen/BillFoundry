using BillFoundry.Application.Configuration;
using BillFoundry.Domain.Catalog;
using BillFoundry.Infrastructure.Demo;
using BillFoundry.Infrastructure.Identity;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

public sealed class DemoSeederTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _sql;

    public DemoSeederTests(SqlServerFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task Seed_creates_fictional_north_beacon_studio_dataset()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        DemoSeeder seeder = provider.GetRequiredService<DemoSeeder>();
        var options = new DemoSeedOptions { Enabled = true, ResetOnStartup = true };

        await seeder.SeedAsync(options, CancellationToken.None);

        using IServiceScope scope = provider.CreateScope();
        BillFoundryDbContext db = scope.ServiceProvider.GetRequiredService<BillFoundryDbContext>();
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.NotNull(await users.FindByEmailAsync(options.AdministratorEmail));
        Assert.NotNull(await users.FindByEmailAsync(options.UserEmail));
        Assert.True(await db.CatalogItems.AnyAsync(item => item.Sku == DemoSeeder.MarkerSku));
        Assert.InRange(await db.Clients.CountAsync(), 10, 20);
        Assert.True(await db.ClientContacts.CountAsync() >= 10);
        Assert.True(await db.Estimates.CountAsync() >= 5);
        Assert.True(await db.Invoices.CountAsync() >= 8);
        Assert.True(await db.InvoicePayments.CountAsync() >= 3);
        Assert.True(await db.AuditEvents.CountAsync() >= 5);
        Assert.Equal("North Beacon Studio", (await db.Organizations.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task Seed_without_reset_does_not_duplicate_business_rows()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        DemoSeeder seeder = provider.GetRequiredService<DemoSeeder>();
        var options = new DemoSeedOptions { Enabled = true, ResetOnStartup = false };

        await seeder.SeedAsync(options, CancellationToken.None);
        await using var countScope = provider.CreateAsyncScope();
        BillFoundryDbContext first = countScope.ServiceProvider.GetRequiredService<BillFoundryDbContext>();
        int clients = await first.Clients.CountAsync();
        int invoices = await first.Invoices.CountAsync();

        await seeder.SeedAsync(options, CancellationToken.None);

        await using var secondScope = provider.CreateAsyncScope();
        BillFoundryDbContext second = secondScope.ServiceProvider.GetRequiredService<BillFoundryDbContext>();
        Assert.Equal(clients, await second.Clients.CountAsync());
        Assert.Equal(invoices, await second.Invoices.CountAsync());
    }
}

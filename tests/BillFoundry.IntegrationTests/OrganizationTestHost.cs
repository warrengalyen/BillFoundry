using System.Security.Claims;
using BillFoundry.Application;
using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Identity;
using BillFoundry.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BillFoundry.IntegrationTests;

internal static class OrganizationTestHost
{
    public static ServiceProvider Create(
        SqlServerFixture sql,
        ICurrentUser currentUser,
        TimeProvider? timeProvider = null,
        IReadOnlyDictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:BillFoundry"] = sql.ConnectionString,
            ["Database:CommandTimeoutSeconds"] = "30",
            ["OrganizationLogoStorage:RootPath"] = sql.LogoRoot,
            ["IdentitySeed:Enabled"] = "false",
            ["DemoMode:Enabled"] = "false",
            ["DemoSeed:Enabled"] = "false"
        };

        if (extra is not null)
        {
            foreach ((string key, string? value) in extra)
            {
                settings[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var environment = new TestHostEnvironment();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddLogging();
        services.AddDataProtection();
        services.AddApplication(configuration);
        services.AddInfrastructure(configuration, environment);
        services.AddScoped<ICurrentUser>(_ => currentUser);
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        return services.BuildServiceProvider();
    }

    public static ClaimsPrincipalCurrentUser Administrator()
    {
        Guid userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, "admin@localhost"),
                new Claim(ClaimTypes.Role, AppRoles.Administrator)
            ],
            authenticationType: "Test"));
        return new ClaimsPrincipalCurrentUser(principal);
    }

    public static ClaimsPrincipalCurrentUser StandardUser()
    {
        Guid userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, "user@localhost"),
                new Claim(ClaimTypes.Role, AppRoles.User)
            ],
            authenticationType: "Test"));
        return new ClaimsPrincipalCurrentUser(principal);
    }

    public static UpdateOrganizationCommand ValidCommand(byte[] rowVersion) =>
        new()
        {
            LegalName = "Acme LLC",
            DisplayName = "Acme",
            AddressLine1 = "10 Main St",
            City = "Springfield",
            Region = "IL",
            PostalCode = "62701",
            Country = "United States",
            Email = "billing@acme.test",
            Phone = "555-0100",
            Website = "https://acme.test",
            TaxIdentifier = "12-3456789",
            DefaultCurrency = "USD",
            DefaultPaymentTermsDays = 30,
            DefaultInvoicePrefix = "INV",
            DefaultEstimatePrefix = "EST",
            DefaultInvoiceNotes = "Thank you.",
            DefaultPaymentInstructions = "Pay by transfer.",
            RowVersion = rowVersion
        };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "BillFoundry.IntegrationTests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

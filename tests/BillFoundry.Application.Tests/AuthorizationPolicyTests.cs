using System.Security.Claims;
using BillFoundry.Application;
using BillFoundry.Application.Configuration;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BillFoundry.Application.Tests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task Administrator_policy_allows_administrator_role()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal user = CreateUser(AppRoles.Administrator);

        AuthorizationResult result = await authorization.AuthorizeAsync(user, AuthorizationPolicies.Administrator);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Administrator_policy_denies_user_role()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal user = CreateUser(AppRoles.User);

        AuthorizationResult result = await authorization.AuthorizeAsync(user, AuthorizationPolicies.Administrator);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Administrator_policy_denies_anonymous_user()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();

        AuthorizationResult result = await authorization.AuthorizeAsync(new ClaimsPrincipal(), AuthorizationPolicies.Administrator);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task NotDemoMode_policy_succeeds_when_demo_mode_is_disabled()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal user = CreateUser(AppRoles.User);

        AuthorizationResult result = await authorization.AuthorizeAsync(user, AuthorizationPolicies.NotDemoMode);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task NotDemoMode_policy_fails_when_demo_mode_is_enabled()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: true);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal user = CreateUser(AppRoles.Administrator);

        AuthorizationResult result = await authorization.AuthorizeAsync(user, AuthorizationPolicies.NotDemoMode);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ManageOrganizationSettings_policy_allows_administrator_role()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal user = CreateUser(AppRoles.Administrator);

        AuthorizationResult result = await authorization.AuthorizeAsync(user, AuthorizationPolicies.ManageOrganizationSettings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ManageOrganizationSettings_policy_denies_user_role()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal user = CreateUser(AppRoles.User);

        AuthorizationResult result = await authorization.AuthorizeAsync(user, AuthorizationPolicies.ManageOrganizationSettings);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ManageClients_policy_allows_user_and_administrator()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();

        AuthorizationResult administrator = await authorization.AuthorizeAsync(
            CreateUser(AppRoles.Administrator),
            AuthorizationPolicies.ManageClients);
        AuthorizationResult user = await authorization.AuthorizeAsync(
            CreateUser(AppRoles.User),
            AuthorizationPolicies.ManageClients);

        Assert.True(administrator.Succeeded);
        Assert.True(user.Succeeded);
    }

    [Fact]
    public async Task ManageClients_policy_denies_anonymous_user()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();

        AuthorizationResult result = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(),
            AuthorizationPolicies.ManageClients);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ManageCatalog_policy_allows_user_and_administrator()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();

        AuthorizationResult administrator = await authorization.AuthorizeAsync(
            CreateUser(AppRoles.Administrator),
            AuthorizationPolicies.ManageCatalog);
        AuthorizationResult user = await authorization.AuthorizeAsync(
            CreateUser(AppRoles.User),
            AuthorizationPolicies.ManageCatalog);

        Assert.True(administrator.Succeeded);
        Assert.True(user.Succeeded);
    }

    [Fact]
    public async Task ManageCatalog_policy_denies_anonymous_user()
    {
        await using ServiceProvider provider = CreateProvider(demoEnabled: false);
        IAuthorizationService authorization = provider.GetRequiredService<IAuthorizationService>();

        AuthorizationResult result = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(),
            AuthorizationPolicies.ManageCatalog);

        Assert.False(result.Succeeded);
    }

    private static ServiceProvider CreateProvider(bool demoEnabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoMode:Enabled"] = demoEnabled ? "true" : "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(configuration);
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CreateUser(string role) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, "user@localhost"),
                new Claim(ClaimTypes.Role, role)
            ],
            authenticationType: "Test"));
}

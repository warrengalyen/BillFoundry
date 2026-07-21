using BillFoundry.Application.Configuration;
using BillFoundry.Domain.Identity;
using BillFoundry.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillFoundry.Infrastructure.Identity;

internal sealed class IdentitySeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<IdentitySeedOptions> options,
    IHostEnvironment environment,
    ILogger<IdentitySeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IdentitySeedOptions seed = options.Value;
        if (!seed.Enabled)
        {
            return;
        }

        if (!environment.IsDevelopment())
        {
            logger.LogWarning("Identity seed is enabled but will not run outside the Development environment.");
            return;
        }

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            await SeedAsync(scope.ServiceProvider, seed, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Identity development seed was skipped because the database was unavailable.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync(
        IServiceProvider services,
        IdentitySeedOptions seed,
        CancellationToken cancellationToken)
    {
        RoleManager<IdentityRole<Guid>> roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        UserManager<ApplicationUser> userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (string roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
            {
                IdentityResult roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)).ConfigureAwait(false);
                EnsureSucceeded(roleResult, $"Failed to create role '{roleName}'.");
            }
        }

        await EnsureUserAsync(
            userManager,
            seed.AdministratorEmail,
            seed.AdministratorPassword,
            AppRoles.Administrator,
            cancellationToken).ConfigureAwait(false);

        await EnsureUserAsync(
            userManager,
            seed.UserEmail,
            seed.UserPassword,
            AppRoles.User,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string? password,
        string role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Skipping seed of {Email} because no password is configured.", email);
            return;
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            IdentityResult createResult = await userManager.CreateAsync(user, password).ConfigureAwait(false);
            EnsureSucceeded(createResult, $"Failed to create seeded user '{email}'.");
            logger.LogInformation("Seeded development user {Email} with role {Role}.", email, role);
        }

        if (!await userManager.IsInRoleAsync(user, role).ConfigureAwait(false))
        {
            IdentityResult roleResult = await userManager.AddToRoleAsync(user, role).ConfigureAwait(false);
            EnsureSucceeded(roleResult, $"Failed to assign role '{role}' to '{email}'.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        string details = string.Join(" ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{message} {details}");
    }
}

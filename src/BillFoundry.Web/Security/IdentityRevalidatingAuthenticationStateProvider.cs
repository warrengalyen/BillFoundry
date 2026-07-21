using System.Security.Claims;
using BillFoundry.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BillFoundry.Web.Security;

internal sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(8);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await ValidateSecurityStampAsync(userManager, authenticationState.User).ConfigureAwait(false);
    }

    private async Task<bool> ValidateSecurityStampAsync(UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
    {
        ApplicationUser? user = await userManager.GetUserAsync(principal).ConfigureAwait(false);
        if (user is null || user.IsDisabled)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        string? principalStamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        string userStamp = await userManager.GetSecurityStampAsync(user).ConfigureAwait(false);
        return string.Equals(principalStamp, userStamp, StringComparison.Ordinal);
    }
}

using Microsoft.AspNetCore.Authorization;

namespace BillFoundry.Application.Security;

public static class AuthorizationPolicies
{
    public const string Administrator = "Administrator";
    public const string ManageOrganizationSettings = "ManageOrganizationSettings";
    public const string ManageClients = "ManageClients";
    public const string NotDemoMode = "NotDemoMode";

    public static void Configure(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(
            Administrator,
            policy => policy.RequireAuthenticatedUser().RequireRole(Domain.Identity.AppRoles.Administrator));

        options.AddPolicy(
            ManageOrganizationSettings,
            policy => policy.RequireAuthenticatedUser().RequireRole(Domain.Identity.AppRoles.Administrator));

        options.AddPolicy(
            ManageClients,
            policy => policy.RequireAuthenticatedUser().RequireRole(
                Domain.Identity.AppRoles.Administrator,
                Domain.Identity.AppRoles.User));

        options.AddPolicy(
            NotDemoMode,
            policy => policy.AddRequirements(new NotDemoModeRequirement()));
    }
}

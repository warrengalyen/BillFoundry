using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;

namespace BillFoundry.Web.Organizations;

internal static class OrganizationLogoEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationLogo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/media/organization-logo", ServeLogoAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOrganizationSettings)
            .WithName("OrganizationLogo");

        return endpoints;
    }

    private static async Task<IResult> ServeLogoAsync(
        IOrganizationSettingsService settings,
        IOrganizationLogoStore store,
        CancellationToken cancellationToken)
    {
        OrganizationSettingsResult result = await settings.GetAsync(cancellationToken);
        if (result.IsForbidden)
        {
            return Results.Forbid();
        }

        if (result.Organization is not { HasLogo: true, LogoFileName: not null, LogoContentType: not null })
        {
            return Results.NotFound();
        }

        Stream? stream = await store.OpenReadAsync(result.Organization.LogoFileName, cancellationToken);
        if (stream is null)
        {
            return Results.NotFound();
        }

        return Results.File(stream, result.Organization.LogoContentType);
    }
}

namespace BillFoundry.Application.Organizations;

/// <summary>
/// Reads and updates the installation's organization profile. Mutations require
/// the <c>ManageOrganizationSettings</c> policy.
/// </summary>
public interface IOrganizationSettingsService
{
    Task<OrganizationSettingsResult> GetAsync(CancellationToken cancellationToken = default);

    Task<OrganizationSettingsResult> UpdateAsync(
        UpdateOrganizationCommand command,
        CancellationToken cancellationToken = default);

    Task<OrganizationSettingsResult> UploadLogoAsync(
        Stream content,
        byte[] rowVersion,
        CancellationToken cancellationToken = default);

    Task<OrganizationSettingsResult> RemoveLogoAsync(
        byte[] rowVersion,
        CancellationToken cancellationToken = default);
}

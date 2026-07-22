namespace BillFoundry.Application.Organizations;

/// <summary>
/// Stores organization logo bytes behind a generated file name. Implementations
/// must reject caller-supplied paths.
/// </summary>
public interface IOrganizationLogoStore
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);
}

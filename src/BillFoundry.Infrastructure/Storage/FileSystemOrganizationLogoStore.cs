using BillFoundry.Application.Configuration;
using BillFoundry.Application.Organizations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BillFoundry.Infrastructure.Storage;

internal sealed class FileSystemOrganizationLogoStore : IOrganizationLogoStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".webp"
    };

    private readonly string _root;

    public FileSystemOrganizationLogoStore(
        IOptions<OrganizationLogoStorageOptions> options,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        string configured = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Organization logo storage root path is not configured.");
        }

        _root = Path.GetFullPath(
            Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured));
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        string safeExtension = NormalizeExtension(extension);
        string fileName = $"{Guid.NewGuid():N}{safeExtension}";
        string path = ResolveSafePath(fileName);

        Directory.CreateDirectory(_root);
        await using FileStream output = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return fileName;
    }

    public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = ResolveSafePath(storedFileName);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = ResolveSafePath(storedFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("A file extension is required.", nameof(extension));
        }

        string trimmed = extension.Trim();
        if (trimmed[0] != '.'
            || trimmed.Contains("..", StringComparison.Ordinal)
            || trimmed.Contains(Path.DirectorySeparatorChar)
            || trimmed.Contains(Path.AltDirectorySeparatorChar)
            || !AllowedExtensions.Contains(trimmed))
        {
            throw new ArgumentException("The file extension is not allowed.", nameof(extension));
        }

        return trimmed.ToLowerInvariant();
    }

    private string ResolveSafePath(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName)
            || storedFileName != Path.GetFileName(storedFileName)
            || storedFileName.Contains("..", StringComparison.Ordinal)
            || storedFileName.Contains(Path.DirectorySeparatorChar)
            || storedFileName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Stored file name must be a generated file name without a path.", nameof(storedFileName));
        }

        string fullRoot = Path.GetFullPath(_root);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            fullRoot += Path.DirectorySeparatorChar;
        }

        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, storedFileName));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The logo path is outside the storage root.");
        }

        return fullPath;
    }
}

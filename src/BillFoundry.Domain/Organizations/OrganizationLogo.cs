namespace BillFoundry.Domain.Organizations;

/// <summary>
/// Metadata for a stored organization logo. The original upload name is not retained.
/// </summary>
public sealed record OrganizationLogo
{
    public const int StoredFileNameMaxLength = 80;
    public const int ContentTypeMaxLength = 100;

    public OrganizationLogo(string storedFileName, string contentType, long sizeBytes)
    {
        StoredFileName = OrganizationText.Required(storedFileName, nameof(storedFileName), StoredFileNameMaxLength);
        if (StoredFileName != Path.GetFileName(StoredFileName)
            || StoredFileName.Contains("..", StringComparison.Ordinal)
            || StoredFileName.Contains(Path.DirectorySeparatorChar)
            || StoredFileName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Logo file name must be a generated file name without a path.", nameof(storedFileName));
        }

        ContentType = OrganizationText.Required(contentType, nameof(contentType), ContentTypeMaxLength);
        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Logo size must be greater than zero.");
        }

        SizeBytes = sizeBytes;
    }

    public string StoredFileName { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }
}

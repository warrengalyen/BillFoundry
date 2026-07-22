namespace BillFoundry.Application.Organizations;

public static class OrganizationLogoRules
{
    public const int MaxSizeBytes = 1_048_576;

    public const string PngContentType = "image/png";
    public const string JpegContentType = "image/jpeg";
    public const string WebpContentType = "image/webp";

    public static string SizeLimitDescription => "1 MB";
}

public sealed class OrganizationLogoInspection
{
    public bool IsValid => Errors.Count == 0;

    public string? ContentType { get; init; }

    public string? Extension { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}

public static class OrganizationLogoInspector
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50];

    public static OrganizationLogoInspection Inspect(ReadOnlySpan<byte> content)
    {
        if (content.Length == 0)
        {
            return Invalid("A logo file is required.");
        }

        if (content.Length > OrganizationLogoRules.MaxSizeBytes)
        {
            return Invalid($"The logo must be {OrganizationLogoRules.SizeLimitDescription} or smaller.");
        }

        if (HasPrefix(content, PngSignature))
        {
            return Valid(OrganizationLogoRules.PngContentType, ".png");
        }

        if (HasPrefix(content, JpegSignature))
        {
            return Valid(OrganizationLogoRules.JpegContentType, ".jpg");
        }

        if (content.Length >= 12
            && HasPrefix(content, RiffSignature)
            && content[8..12].SequenceEqual(WebpSignature))
        {
            return Valid(OrganizationLogoRules.WebpContentType, ".webp");
        }

        return Invalid("The logo must be a PNG, JPEG, or WebP image.");
    }

    private static OrganizationLogoInspection Valid(string contentType, string extension) =>
        new()
        {
            ContentType = contentType,
            Extension = extension
        };

    private static OrganizationLogoInspection Invalid(string error) =>
        new()
        {
            Errors = [error]
        };

    private static bool HasPrefix(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) =>
        content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature);
}

using BillFoundry.Application.Organizations;

namespace BillFoundry.Application.Tests;

public sealed class OrganizationLogoInspectorTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public void Inspect_accepts_png_signature()
    {
        OrganizationLogoInspection result = OrganizationLogoInspector.Inspect(Png);

        Assert.True(result.IsValid);
        Assert.Equal(OrganizationLogoRules.PngContentType, result.ContentType);
        Assert.Equal(".png", result.Extension);
    }

    [Fact]
    public void Inspect_accepts_jpeg_signature()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

        OrganizationLogoInspection result = OrganizationLogoInspector.Inspect(jpeg);

        Assert.True(result.IsValid);
        Assert.Equal(OrganizationLogoRules.JpegContentType, result.ContentType);
        Assert.Equal(".jpg", result.Extension);
    }

    [Fact]
    public void Inspect_accepts_webp_signature()
    {
        byte[] webp =
        [
            0x52, 0x49, 0x46, 0x46, 0x18, 0x00, 0x00, 0x00,
            0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20
        ];

        OrganizationLogoInspection result = OrganizationLogoInspector.Inspect(webp);

        Assert.True(result.IsValid);
        Assert.Equal(OrganizationLogoRules.WebpContentType, result.ContentType);
        Assert.Equal(".webp", result.Extension);
    }

    [Fact]
    public void Inspect_rejects_mismatched_content()
    {
        OrganizationLogoInspection result = OrganizationLogoInspector.Inspect("not-an-image"u8.ToArray());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("PNG, JPEG, or WebP", StringComparison.Ordinal));
    }

    [Fact]
    public void Inspect_rejects_oversized_payload()
    {
        byte[] oversized = new byte[OrganizationLogoRules.MaxSizeBytes + 1];
        Png.CopyTo(oversized, 0);

        OrganizationLogoInspection result = OrganizationLogoInspector.Inspect(oversized);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("1 MB", StringComparison.Ordinal));
    }

    [Fact]
    public void Inspect_rejects_empty_payload()
    {
        OrganizationLogoInspection result = OrganizationLogoInspector.Inspect([]);

        Assert.False(result.IsValid);
    }
}

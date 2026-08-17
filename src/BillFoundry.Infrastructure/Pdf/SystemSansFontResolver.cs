using System.Reflection;
using PdfSharp.Fonts;

namespace BillFoundry.Infrastructure.Pdf;

internal sealed class SystemSansFontResolver : IFontResolver
{
    internal const string FaceName = "BillFoundrySans";

    private const string RegularResource = "BillFoundry.Infrastructure.Pdf.Fonts.LiberationSans-Regular.ttf";
    private const string BoldResource = "BillFoundry.Infrastructure.Pdf.Fonts.LiberationSans-Bold.ttf";
    private const string ItalicResource = "BillFoundry.Infrastructure.Pdf.Fonts.LiberationSans-Italic.ttf";
    private const string BoldItalicResource = "BillFoundry.Infrastructure.Pdf.Fonts.LiberationSans-BoldItalic.ttf";

    private static readonly object Gate = new();
    private static readonly Assembly ResourceAssembly = typeof(SystemSansFontResolver).Assembly;
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            GlobalFontSettings.FontResolver = new SystemSansFontResolver();
            _registered = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string suffix = (isBold, isItalic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular"
        };

        return new FontResolverInfo($"{FaceName}#{suffix}");
    }

    public byte[] GetFont(string faceName) =>
        faceName switch
        {
            FaceName + "#BoldItalic" => ReadEmbedded(BoldItalicResource),
            FaceName + "#Bold" => ReadEmbedded(BoldResource),
            FaceName + "#Italic" => ReadEmbedded(ItalicResource),
            _ => ReadEmbedded(RegularResource)
        };

    private static byte[] ReadEmbedded(string resourceName)
    {
        using Stream? stream = ResourceAssembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded PDF font '{resourceName}' is missing from {ResourceAssembly.GetName().Name}.");
        }

        using var buffer = new MemoryStream(capacity: (int)stream.Length);
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

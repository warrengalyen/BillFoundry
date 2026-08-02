using PdfSharp.Fonts;

namespace BillFoundry.Infrastructure.Pdf;

internal sealed class SystemSansFontResolver : IFontResolver
{
    internal const string FaceName = "BillFoundrySans";

    private static readonly object Gate = new();
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

    public byte[] GetFont(string faceName)
    {
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string? file = faceName switch
        {
            FaceName + "#BoldItalic" => FirstExisting(fonts, "arialbi.ttf", "ArialBI.ttf", "LiberationSans-BoldItalic.ttf"),
            FaceName + "#Bold" => FirstExisting(fonts, "arialbd.ttf", "ArialBd.ttf", "LiberationSans-Bold.ttf"),
            FaceName + "#Italic" => FirstExisting(fonts, "ariali.ttf", "ArialI.ttf", "LiberationSans-Italic.ttf"),
            _ => FirstExisting(fonts, "arial.ttf", "Arial.ttf", "LiberationSans-Regular.ttf", "DejaVuSans.ttf")
        };

        if (file is null)
        {
            throw new InvalidOperationException(
                "A sans-serif TrueType font (Arial or Liberation Sans) is required to generate PDF documents.");
        }

        return File.ReadAllBytes(file);
    }

    private static string? FirstExisting(string directory, params string[] names)
    {
        foreach (string name in names)
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        string linuxFonts = "/usr/share/fonts";
        if (Directory.Exists(linuxFonts))
        {
            foreach (string name in names)
            {
                string? match = Directory.EnumerateFiles(linuxFonts, name, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return null;
    }
}

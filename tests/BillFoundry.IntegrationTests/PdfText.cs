using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace BillFoundry.IntegrationTests;

internal static class PdfText
{
    public static string Read(byte[] content)
    {
        using PdfDocument document = PdfDocument.Open(content);
        return string.Join('\n', document.GetPages().Select(page => page.Text));
    }
}

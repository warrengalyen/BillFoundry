using BillFoundry.Application.Documents;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace BillFoundry.Infrastructure.Pdf;

internal sealed class FinancialPdfModel
{
    public required string Title { get; init; }

    public required string Number { get; init; }

    public required string StatusLabel { get; init; }

    public required DateOnly IssueDate { get; init; }

    public DateOnly? SecondaryDate { get; init; }

    public string? SecondaryDateLabel { get; init; }

    public string? ReferenceLabel { get; init; }

    public string? ReferenceValue { get; init; }

    public required DocumentIssuerModel Issuer { get; init; }

    public required DocumentPartyModel Client { get; init; }

    public required string CurrencyCode { get; init; }

    public required IReadOnlyList<DocumentLineModel> Lines { get; init; }

    public required decimal Subtotal { get; init; }

    public required decimal Discount { get; init; }

    public required decimal TaxRatePercent { get; init; }

    public required decimal Tax { get; init; }

    public required decimal Total { get; init; }

    public decimal? AmountPaid { get; init; }

    public decimal? BalanceDue { get; init; }

    public string? Notes { get; init; }

    public string? ClosingTitle { get; init; }

    public string? ClosingText { get; init; }

    public required bool ShowVoidMark { get; init; }
}

internal static class FinancialDocumentPdfWriter
{
    private const double Margin = 50;
    private const double PageWidth = 612;
    private const double PageHeight = 792;
    private const double ContentWidth = PageWidth - (Margin * 2);

    private static readonly XColor Ink = XColor.FromArgb(32, 32, 32);
    private static readonly XColor Muted = XColor.FromArgb(90, 90, 90);
    private static readonly XColor Rule = XColor.FromArgb(180, 180, 180);
    private static readonly XColor HeaderFill = XColor.FromArgb(240, 240, 240);
    private static readonly XColor VoidTint = XColor.FromArgb(36, 90, 90, 90);

    public static byte[] Write(FinancialPdfModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        SystemSansFontResolver.EnsureRegistered();

        using var document = new PdfDocument();
        document.Info.Title = $"{model.Title} {model.Number}";
        document.Info.Author = model.Issuer.LegalName;
        document.Info.Creator = "BillFoundry";

        XFont titleFont = CreateFont(18, XFontStyleEx.Bold);
        XFont headingFont = CreateFont(11, XFontStyleEx.Bold);
        XFont bodyFont = CreateFont(9, XFontStyleEx.Regular);
        XFont bodyBold = CreateFont(9, XFontStyleEx.Bold);
        XFont smallFont = CreateFont(8, XFontStyleEx.Regular);

        PdfPage page = AddPage(document);
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double y = Margin;

        DrawHeader(gfx, model, titleFont, headingFont, smallFont, ref y);
        DrawMeta(gfx, model, headingFont, bodyFont, ref y);
        DrawClient(gfx, model, headingFont, bodyFont, ref y);
        DrawLines(document, ref gfx, ref page, model, bodyFont, bodyBold, smallFont, ref y);
        DrawTotals(document, ref gfx, ref page, model, bodyFont, headingFont, ref y);
        DrawNotes(document, ref gfx, ref page, model, headingFont, bodyFont, ref y);

        if (model.ShowVoidMark)
        {
            DrawVoidMark(gfx, titleFont);
        }

        gfx.Dispose();
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void DrawHeader(
        XGraphics gfx,
        FinancialPdfModel model,
        XFont titleFont,
        XFont headingFont,
        XFont smallFont,
        ref double y)
    {
        double issuerX = Margin;
        double logoBottom = y;

        if (model.Issuer.LogoBytes is { Length: > 0 } bytes
            && TryLoadImage(bytes, out XImage? logo)
            && logo is not null)
        {
            using (logo)
            {
                double scale = Math.Min(140d / logo.PointWidth, 48d / logo.PointHeight);
                double width = logo.PointWidth * scale;
                double height = logo.PointHeight * scale;
                gfx.DrawImage(logo, Margin, y, width, height);
                logoBottom = y + height;
                issuerX = Margin + width + 12;
            }
        }

        string issuerName = string.IsNullOrWhiteSpace(model.Issuer.DisplayName)
            ? model.Issuer.LegalName
            : model.Issuer.DisplayName;
        gfx.DrawString(issuerName, headingFont, Brush(Ink), new XRect(issuerX, y, 250, 14), XStringFormats.TopLeft);
        double textY = y + 16;
        foreach (string line in IssuerLines(model.Issuer))
        {
            gfx.DrawString(line, smallFont, Brush(Muted), new XRect(issuerX, textY, 250, 11), XStringFormats.TopLeft);
            textY += 11;
        }

        double right = PageWidth - Margin;
        gfx.DrawString(model.Title, titleFont, Brush(Ink), new XRect(right - 210, y, 210, 22), XStringFormats.TopRight);
        gfx.DrawString(model.Number, headingFont, Brush(Ink), new XRect(right - 210, y + 22, 210, 14), XStringFormats.TopRight);
        gfx.DrawString(model.StatusLabel, smallFont, Brush(Muted), new XRect(right - 210, y + 38, 210, 12), XStringFormats.TopRight);

        y = Math.Max(logoBottom, Math.Max(textY, y + 54)) + 10;
        DrawRule(gfx, y);
        y += 14;
    }

    private static void DrawMeta(XGraphics gfx, FinancialPdfModel model, XFont headingFont, XFont bodyFont, ref double y)
    {
        DrawLabeledValue(gfx, "Issue date", DocumentText.Date(model.IssueDate), headingFont, bodyFont, Margin, y);
        if (model.SecondaryDate is DateOnly secondary && !string.IsNullOrWhiteSpace(model.SecondaryDateLabel))
        {
            DrawLabeledValue(gfx, model.SecondaryDateLabel, DocumentText.Date(secondary), headingFont, bodyFont, Margin + 180, y);
        }

        if (!string.IsNullOrWhiteSpace(model.ReferenceValue) && !string.IsNullOrWhiteSpace(model.ReferenceLabel))
        {
            DrawLabeledValue(gfx, model.ReferenceLabel, model.ReferenceValue, headingFont, bodyFont, Margin + 360, y);
        }

        y += 34;
    }

    private static void DrawClient(XGraphics gfx, FinancialPdfModel model, XFont headingFont, XFont bodyFont, ref double y)
    {
        gfx.DrawString("Bill to", headingFont, Brush(Ink), new XPoint(Margin, y + 9));
        y += 14;
        gfx.DrawString(model.Client.Name, headingFont, Brush(Ink), new XPoint(Margin, y + 9));
        y += 13;
        if (!string.IsNullOrWhiteSpace(model.Client.Code))
        {
            gfx.DrawString(model.Client.Code, bodyFont, Brush(Muted), new XPoint(Margin, y + 9));
            y += 12;
        }

        if (!string.IsNullOrWhiteSpace(model.Client.Email))
        {
            gfx.DrawString(model.Client.Email, bodyFont, Brush(Ink), new XPoint(Margin, y + 9));
            y += 12;
        }

        if (!string.IsNullOrWhiteSpace(model.Client.Phone))
        {
            gfx.DrawString(model.Client.Phone, bodyFont, Brush(Ink), new XPoint(Margin, y + 9));
            y += 12;
        }

        foreach (string line in model.Client.AddressLines)
        {
            gfx.DrawString(line, bodyFont, Brush(Ink), new XPoint(Margin, y + 9));
            y += 12;
        }

        y += 8;
    }

    private static void DrawLines(
        PdfDocument document,
        ref XGraphics gfx,
        ref PdfPage page,
        FinancialPdfModel model,
        XFont bodyFont,
        XFont bodyBold,
        XFont smallFont,
        ref double y)
    {
        double[] columns = [248, 54, 70, 70, 70];
        string[] headers = ["Description", "Qty", "Unit", "Rate", "Amount"];
        EnsureSpace(document, ref gfx, ref page, ref y, 28);
        DrawTableHeader(gfx, y, columns, headers, bodyBold);
        y += 18;

        if (model.Lines.Count == 0)
        {
            gfx.DrawString("No line items.", bodyFont, Brush(Muted), new XPoint(Margin + 4, y + 9));
            y += 20;
            return;
        }

        foreach (DocumentLineModel line in model.Lines)
        {
            IReadOnlyList<string> wrapped = Wrap(gfx, bodyFont, line.Description, columns[0] - 8);
            double rowHeight = Math.Max(16, wrapped.Count * 11 + 6);
            EnsureSpace(document, ref gfx, ref page, ref y, rowHeight + 4);
            double textY = y + 11;
            foreach (string wrap in wrapped)
            {
                gfx.DrawString(wrap, bodyFont, Brush(Ink), new XPoint(Margin + 4, textY));
                textY += 11;
            }

            double x = Margin + columns[0];
            gfx.DrawString(DocumentText.Quantity(line.Quantity), bodyFont, Brush(Ink), new XRect(x, y, columns[1] - 4, rowHeight), XStringFormats.CenterRight);
            x += columns[1];
            gfx.DrawString(line.UnitLabel, bodyFont, Brush(Ink), new XRect(x + 2, y, columns[2] - 4, rowHeight), XStringFormats.CenterLeft);
            x += columns[2];
            gfx.DrawString(DocumentText.Money(line.UnitPrice, model.CurrencyCode), smallFont, Brush(Ink), new XRect(x, y, columns[3] - 4, rowHeight), XStringFormats.CenterRight);
            x += columns[3];
            gfx.DrawString(DocumentText.Money(line.LineAmount, model.CurrencyCode), smallFont, Brush(Ink), new XRect(x, y, columns[4] - 4, rowHeight), XStringFormats.CenterRight);
            y += rowHeight;
            DrawRule(gfx, y, 0.3);
            y += 3;
        }

        y += 8;
    }

    private static void DrawTotals(
        PdfDocument document,
        ref XGraphics gfx,
        ref PdfPage page,
        FinancialPdfModel model,
        XFont bodyFont,
        XFont headingFont,
        ref double y)
    {
        var rows = new List<(string Label, string Value, bool Emphasize)>
        {
            ("Subtotal", DocumentText.Money(model.Subtotal, model.CurrencyCode), false),
            ("Discount", DocumentText.Money(model.Discount, model.CurrencyCode), false),
            ($"Tax ({DocumentText.Percent(model.TaxRatePercent)})", DocumentText.Money(model.Tax, model.CurrencyCode), false),
            ("Total", DocumentText.Money(model.Total, model.CurrencyCode), true)
        };

        if (model.AmountPaid is decimal paid)
        {
            rows.Add(("Amount paid", DocumentText.Money(paid, model.CurrencyCode), false));
        }

        if (model.BalanceDue is decimal balance)
        {
            rows.Add(("Balance due", DocumentText.Money(balance, model.CurrencyCode), true));
        }

        EnsureSpace(document, ref gfx, ref page, ref y, rows.Count * 16 + 8);
        double labelX = Margin + 280;
        double valueX = Margin + 400;
        foreach ((string label, string value, bool emphasize) in rows)
        {
            XFont font = emphasize ? headingFont : bodyFont;
            gfx.DrawString(label, font, Brush(Ink), new XRect(labelX, y, 110, 14), XStringFormats.CenterLeft);
            gfx.DrawString(value, font, Brush(Ink), new XRect(valueX, y, ContentWidth - 400, 14), XStringFormats.CenterRight);
            y += 16;
        }

        y += 10;
    }

    private static void DrawNotes(
        PdfDocument document,
        ref XGraphics gfx,
        ref PdfPage page,
        FinancialPdfModel model,
        XFont headingFont,
        XFont bodyFont,
        ref double y)
    {
        DrawParagraph(document, ref gfx, ref page, "Notes", model.Notes, headingFont, bodyFont, ref y);
        DrawParagraph(document, ref gfx, ref page, model.ClosingTitle, model.ClosingText, headingFont, bodyFont, ref y);
    }

    private static void DrawParagraph(
        PdfDocument document,
        ref XGraphics gfx,
        ref PdfPage page,
        string? title,
        string? text,
        XFont headingFont,
        XFont bodyFont,
        ref double y)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        IReadOnlyList<string> lines = Wrap(gfx, bodyFont, text, ContentWidth);
        EnsureSpace(document, ref gfx, ref page, ref y, 20 + (lines.Count * 12));
        gfx.DrawString(title, headingFont, Brush(Ink), new XPoint(Margin, y + 9));
        y += 16;
        foreach (string line in lines)
        {
            EnsureSpace(document, ref gfx, ref page, ref y, 14);
            gfx.DrawString(line, bodyFont, Brush(Ink), new XPoint(Margin, y + 9));
            y += 12;
        }

        y += 8;
    }

    private static void DrawVoidMark(XGraphics gfx, XFont titleFont)
    {
        gfx.Save();
        gfx.TranslateTransform(PageWidth / 2, PageHeight / 2);
        gfx.RotateTransform(-32);
        gfx.DrawString("VOID", titleFont, new XSolidBrush(VoidTint), new XRect(-180, -20, 360, 40), XStringFormats.Center);
        gfx.Restore();
    }

    private static void DrawTableHeader(XGraphics gfx, double y, double[] columns, string[] headers, XFont font)
    {
        gfx.DrawRectangle(new XSolidBrush(HeaderFill), Margin, y, ContentWidth, 16);
        double x = Margin;
        for (int index = 0; index < headers.Length; index++)
        {
            XStringFormat format = index is 1 or 3 or 4 ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
            gfx.DrawString(headers[index], font, Brush(Ink), new XRect(x + 2, y, columns[index] - 4, 16), format);
            x += columns[index];
        }
    }

    private static void DrawLabeledValue(XGraphics gfx, string label, string value, XFont labelFont, XFont valueFont, double x, double y)
    {
        gfx.DrawString(label, labelFont, Brush(Muted), new XPoint(x, y + 9));
        gfx.DrawString(value, valueFont, Brush(Ink), new XPoint(x, y + 22));
    }

    private static void DrawRule(XGraphics gfx, double y, double width = 0.6) =>
        gfx.DrawLine(new XPen(Rule, width), Margin, y, PageWidth - Margin, y);

    private static void EnsureSpace(
        PdfDocument document,
        ref XGraphics gfx,
        ref PdfPage page,
        ref double y,
        double needed)
    {
        if (y + needed <= PageHeight - Margin)
        {
            return;
        }

        gfx.Dispose();
        page = AddPage(document);
        gfx = XGraphics.FromPdfPage(page);
        y = Margin;
    }

    private static PdfPage AddPage(PdfDocument document)
    {
        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(PageWidth);
        page.Height = XUnit.FromPoint(PageHeight);
        return page;
    }

    private static IReadOnlyList<string> IssuerLines(DocumentIssuerModel issuer)
    {
        var lines = new List<string>();
        if (!string.Equals(issuer.LegalName, issuer.DisplayName, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(issuer.LegalName))
        {
            lines.Add(issuer.LegalName);
        }

        lines.AddRange(issuer.AddressLines);
        if (!string.IsNullOrWhiteSpace(issuer.Email))
        {
            lines.Add(issuer.Email);
        }

        if (!string.IsNullOrWhiteSpace(issuer.Phone))
        {
            lines.Add(issuer.Phone);
        }

        if (!string.IsNullOrWhiteSpace(issuer.Website))
        {
            lines.Add(issuer.Website);
        }

        if (!string.IsNullOrWhiteSpace(issuer.TaxId))
        {
            lines.Add("Tax ID " + issuer.TaxId);
        }

        return lines;
    }

    private static IReadOnlyList<string> Wrap(XGraphics gfx, XFont font, string text, double width)
    {
        string[] words = text.ReplaceLineEndings(" ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [string.Empty];
        }

        var lines = new List<string>();
        string current = words[0];
        for (int index = 1; index < words.Length; index++)
        {
            string candidate = current + " " + words[index];
            if (gfx.MeasureString(candidate, font).Width <= width)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = words[index];
            }
        }

        lines.Add(current);
        return lines;
    }

    private static bool TryLoadImage(byte[] bytes, out XImage? image)
    {
        try
        {
            var stream = new MemoryStream(bytes, writable: false);
            image = XImage.FromStream(stream);
            return true;
        }
        catch (Exception)
        {
            image = null;
            return false;
        }
    }

    private static XFont CreateFont(double size, XFontStyleEx style) =>
        new(SystemSansFontResolver.FaceName, size, style);

    private static XSolidBrush Brush(XColor color) => new(color);
}

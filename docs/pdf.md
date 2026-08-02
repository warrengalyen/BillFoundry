# PDF documents

BillFoundry generates US Letter PDF invoices and estimates in memory and
returns them as downloads. Physical storage paths are never shown to users.
Document numbers and persisted totals come from the saved invoice or estimate;
catalog prices and organization defaults are not recomputed at print time.

## Library

Community Edition uses **PDFsharp** (`PDFsharp` NuGet package, version 6.2.4).

| Topic | Detail |
| --- | --- |
| License | MIT |
| Publisher | empira Software GmbH |
| Targets | `net8.0`, `net9.0`, `net10.0`, `netstandard2.0` |
| Maintenance | Actively released (6.2.4, January 2026) with Core, GDI, and WPF builds |
| Package | The Core package, which does not require Windows GDI+ or WPF |

### Options considered

**PDFsharp 6.2 (selected).** MIT, OSI-approved, compatible with AGPLv3
distribution and with a later separately licensed commercial edition. The
Core build runs on .NET 10. Layout is drawn with the PDFsharp API so images
can be loaded from streams without writing temporary files.

**QuestPDF.** Mature layout API and active maintenance, but it is
source-available under a Community License that is not OSI-approved. Open-source
projects can qualify for free use; a future commercial BillFoundry edition
could need a paid QuestPDF license. That extra obligation is avoided by using
MIT PDFsharp.

**iText 8 Community.** AGPL, which matches this repository but would force a
paid iText license (or removal) for a separately licensed commercial product.

**Browser HTML-to-PDF (Playwright/Chromium).** Apache-2.0 tooling with a large
runtime and operational cost. Unnecessary for letterhead invoices.

**PdfPig.** Apache-2.0 and useful for reading PDFs in tests. It does not
generate documents.

## Architecture

```
Web download endpoint
  -> IInvoiceDocumentService / IEstimateDocumentService  (Application contract)
       authorize ManageInvoices / ManageEstimates
       load invoice or estimate, organization letterhead, optional logo bytes
       map persisted values into a document model
       -> IInvoiceDocumentGenerator / IEstimateDocumentGenerator
            PDFsharp implementation in Infrastructure
            returns GeneratedDocument { FileName, ContentType, Content }
  -> HTTP file result (Content-Disposition filename only)
```

Generators live in Infrastructure. Domain and Application do not reference
PDFsharp. Application defines the generator and download-service contracts plus
the document models. Razor pages link to the download URLs; they do not build
PDFs.

Organization letterhead is read from the organization row as part of invoice or
estimate authorization. Users who can manage invoices do not need the
administrator-only organization-settings policy to print a document.

Logo bytes are copied from `IOrganizationLogoStore` into memory. If the stored
image cannot be embedded (for example WebP), the PDF is still produced without
a logo.

## Layout

- US Letter, portrait, conservative margins
- Dark gray type, light gray rules, no saturated color
- Readable in grayscale and on paper
- Issuer block (logo when available, legal name, address, contact)
- Document title, number, status, issue date, due date or expiration
- Client block from the invoice snapshot (invoices) or current client
  (estimates, which do not snapshot billed-to identity)
- Line table: description, quantity, unit, rate, amount
- Totals from persisted fields, including amount paid and balance due on invoices
- Notes and payment instructions (invoices) or terms (estimates)
- Void invoices receive a light VOID marking

Currency amounts use the document's stored currency code and two decimal
places (`USD 1,250.00`). Dates use a US English long form (`August 22, 2026`).

## File names

`DocumentFileName` builds names such as `invoice-INV-0001.pdf`. The document
number is stripped to letters, digits, dots, underscores, and hyphens. Empty
or unsafe numbers fall back to `invoice.pdf` or `estimate.pdf`. The HTTP
response uses that file name only.

## Customization and maintenance

- Change layout in `FinancialDocumentPdfWriter` (Infrastructure). Keep
  invoice and estimate field mapping in the PDF generators so persisted
  values stay explicit.
- PDFsharp Core resolves fonts through `SystemSansFontResolver`, which loads
  Arial from the OS fonts directory on Windows. Deployments without Arial need
  a resolver that supplies a licensed sans-serif TTF.
- Upgrade PDFsharp with `Directory.Packages.props`. Re-run generator tests
  after upgrades; they assert `%PDF` headers and extracted field text via
  PdfPig (test-only, Apache-2.0).
- Do not add QuestPDF or iText without revisiting AGPL and commercial
  licensing.

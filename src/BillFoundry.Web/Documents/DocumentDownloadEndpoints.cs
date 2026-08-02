using BillFoundry.Application.Documents;
using BillFoundry.Application.Security;

namespace BillFoundry.Web.Documents;

internal static class DocumentDownloadEndpoints
{
    public static IEndpointRouteBuilder MapDocumentDownloads(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/Invoices/{id:guid}/pdf", DownloadInvoiceAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageInvoices)
            .WithName("InvoicePdf");

        endpoints.MapGet("/Estimates/{id:guid}/pdf", DownloadEstimateAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageEstimates)
            .WithName("EstimatePdf");

        return endpoints;
    }

    private static async Task<IResult> DownloadInvoiceAsync(
        Guid id,
        IInvoiceDocumentService documents,
        CancellationToken cancellationToken)
    {
        DocumentResult result = await documents.GenerateAsync(id, cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> DownloadEstimateAsync(
        Guid id,
        IEstimateDocumentService documents,
        CancellationToken cancellationToken)
    {
        DocumentResult result = await documents.GenerateAsync(id, cancellationToken);
        return ToHttpResult(result);
    }

    private static IResult ToHttpResult(DocumentResult result)
    {
        if (result.IsForbidden)
        {
            return Results.Forbid();
        }

        if (result.IsNotFound)
        {
            return Results.NotFound();
        }

        if (!result.Succeeded || result.Document is null)
        {
            return Results.BadRequest();
        }

        return Results.File(result.Document.Content, result.Document.ContentType, result.Document.FileName);
    }
}

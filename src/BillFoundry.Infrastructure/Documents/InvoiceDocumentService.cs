using BillFoundry.Application.Documents;
using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Invoices;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Pdf;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Documents;

internal sealed class InvoiceDocumentService(
    BillFoundryDbContext dbContext,
    IInvoiceDocumentGenerator generator,
    IOrganizationLogoStore logoStore,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IInvoiceDocumentService
{
    public async Task<DocumentResult> GenerateAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.ManageInvoices)
            .ConfigureAwait(false);
        if (!authorization.Succeeded)
        {
            return DocumentResult.Forbidden("You are not allowed to manage invoices.");
        }

        Invoice? invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(entity => entity.Lines)
            .FirstOrDefaultAsync(entity => entity.Id == invoiceId, cancellationToken)
            .ConfigureAwait(false);
        if (invoice is null)
        {
            return DocumentResult.NotFound("The invoice was not found.");
        }

        DocumentIssuerModel issuer = await LoadIssuerAsync(cancellationToken).ConfigureAwait(false);
        var clientRow = await dbContext.Clients.AsNoTracking()
            .Where(entity => entity.Id == invoice.ClientId)
            .Select(entity => new { entity.Phone, entity.BillingAddress })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        InvoiceStatus effective = invoice.EffectiveStatus(today);

        InvoiceDocumentModel model = new()
        {
            Issuer = issuer,
            Client = DocumentLayout.Party(
                invoice.ClientSnapshot.Name,
                invoice.ClientSnapshot.Code,
                invoice.ClientSnapshot.Email,
                clientRow?.Phone,
                clientRow?.BillingAddress),
            Number = invoice.Number,
            StatusLabel = InvoiceStatusRules.Label(effective),
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            PurchaseOrder = invoice.PurchaseOrder,
            CurrencyCode = invoice.Currency.Value,
            Lines =
            [
                .. invoice.Lines
                    .OrderBy(line => line.SortOrder)
                    .Select(line => DocumentLayout.Line(
                        line.Description,
                        line.Quantity,
                        line.Unit,
                        line.UnitPrice,
                        line.LineAmount))
            ],
            Subtotal = invoice.Subtotal,
            Discount = invoice.Discount,
            TaxRatePercent = invoice.TaxRatePercent,
            Tax = invoice.Tax,
            Total = invoice.Total,
            AmountPaid = invoice.AmountPaid,
            BalanceDue = invoice.BalanceDue,
            Notes = invoice.Notes,
            PaymentInstructions = invoice.PaymentInstructions,
            IsVoid = invoice.Status is InvoiceStatus.Void
        };

        return DocumentResult.Success(generator.Generate(model));
    }

    private async Task<DocumentIssuerModel> LoadIssuerAsync(CancellationToken cancellationToken)
    {
        Organization? organization = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == Organization.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        byte[]? logoBytes = null;
        if (organization?.Logo?.StoredFileName is string storedFileName)
        {
            Stream? stream = await logoStore.OpenReadAsync(storedFileName, cancellationToken).ConfigureAwait(false);
            if (stream is not null)
            {
                await using (stream.ConfigureAwait(false))
                {
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                    logoBytes = buffer.ToArray();
                }
            }
        }

        return DocumentLayout.Issuer(organization, logoBytes);
    }
}

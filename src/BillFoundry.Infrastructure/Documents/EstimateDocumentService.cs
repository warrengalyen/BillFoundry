using BillFoundry.Application.Documents;
using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Pdf;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Documents;

internal sealed class EstimateDocumentService(
    BillFoundryDbContext dbContext,
    IEstimateDocumentGenerator generator,
    IOrganizationLogoStore logoStore,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser) : IEstimateDocumentService
{
    public async Task<DocumentResult> GenerateAsync(Guid estimateId, CancellationToken cancellationToken = default)
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.ManageEstimates)
            .ConfigureAwait(false);
        if (!authorization.Succeeded)
        {
            return DocumentResult.Forbidden("You are not allowed to manage estimates.");
        }

        Estimate? estimate = await dbContext.Estimates
            .AsNoTracking()
            .Include(entity => entity.Lines)
            .FirstOrDefaultAsync(entity => entity.Id == estimateId, cancellationToken)
            .ConfigureAwait(false);
        if (estimate is null)
        {
            return DocumentResult.NotFound("The estimate was not found.");
        }

        Client? client = await dbContext.Clients.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == estimate.ClientId, cancellationToken)
            .ConfigureAwait(false);

        DocumentIssuerModel issuer = await LoadIssuerAsync(cancellationToken).ConfigureAwait(false);

        EstimateDocumentModel model = new()
        {
            Issuer = issuer,
            Client = DocumentLayout.Party(
                client?.Name ?? "Client",
                client?.Code,
                client?.Email,
                client?.Phone,
                client?.BillingAddress),
            Number = estimate.Number,
            StatusLabel = EstimateStatusRules.Label(estimate.Status),
            IssueDate = estimate.IssueDate,
            ExpirationDate = estimate.ExpirationDate,
            CurrencyCode = estimate.Currency.Value,
            Lines =
            [
                .. estimate.Lines
                    .OrderBy(line => line.SortOrder)
                    .Select(line => DocumentLayout.Line(
                        line.Description,
                        line.Quantity,
                        line.Unit,
                        line.UnitPrice,
                        line.LineAmount))
            ],
            Subtotal = estimate.Subtotal,
            Discount = estimate.Discount,
            TaxRatePercent = estimate.TaxRatePercent,
            Tax = estimate.Tax,
            Total = estimate.Total,
            Notes = estimate.Notes,
            Terms = estimate.Terms
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

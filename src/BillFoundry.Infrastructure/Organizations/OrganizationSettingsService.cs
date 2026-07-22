using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Organizations;

internal sealed class OrganizationSettingsService(
    BillFoundryDbContext dbContext,
    IOrganizationLogoStore logoStore,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser) : IOrganizationSettingsService
{
    public async Task<OrganizationSettingsResult> GetAsync(CancellationToken cancellationToken = default)
    {
        OrganizationSettingsResult? forbidden = await ForbidIfUnauthorizedAsync().ConfigureAwait(false);
        if (forbidden is not null)
        {
            return forbidden;
        }

        Organization organization = await GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        return OrganizationSettingsResult.Success(OrganizationSettingsDto.From(organization));
    }

    public async Task<OrganizationSettingsResult> UpdateAsync(
        UpdateOrganizationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        OrganizationSettingsResult? forbidden = await ForbidIfUnauthorizedAsync().ConfigureAwait(false);
        if (forbidden is not null)
        {
            return forbidden;
        }

        IReadOnlyList<string> errors = OrganizationSettingsValidator.Validate(command);
        if (errors.Count > 0)
        {
            Organization current = await GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
            return OrganizationSettingsResult.Invalid(errors, OrganizationSettingsDto.From(current));
        }

        Organization organization = await GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        ApplyRowVersion(organization, command.RowVersion);

        organization.UpdateProfile(
            command.LegalName,
            command.DisplayName,
            PostalAddress.Create(
                command.AddressLine1,
                command.AddressLine2,
                command.City,
                command.Region,
                command.PostalCode,
                command.Country),
            command.Email,
            command.Phone,
            command.Website,
            command.TaxIdentifier,
            CurrencyCode.Parse(command.DefaultCurrency),
            command.DefaultPaymentTermsDays,
            DocumentPrefix.Parse(command.DefaultInvoicePrefix),
            DocumentPrefix.Parse(command.DefaultEstimatePrefix),
            command.DefaultInvoiceNotes,
            command.DefaultPaymentInstructions);

        return await SaveAsync(organization, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OrganizationSettingsResult> UploadLogoAsync(
        Stream content,
        byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(rowVersion);

        OrganizationSettingsResult? forbidden = await ForbidIfUnauthorizedAsync().ConfigureAwait(false);
        if (forbidden is not null)
        {
            return forbidden;
        }

        MemoryStream buffer;
        try
        {
            buffer = await CopyLimitedAsync(content, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            Organization current = await GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
            return OrganizationSettingsResult.Invalid(
                [$"The logo must be {OrganizationLogoRules.SizeLimitDescription} or smaller."],
                OrganizationSettingsDto.From(current));
        }

        await using (buffer)
        {
            OrganizationLogoInspection inspection = OrganizationLogoInspector.Inspect(buffer.ToArray());
            if (!inspection.IsValid)
            {
                Organization current = await GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
                return OrganizationSettingsResult.Invalid(inspection.Errors, OrganizationSettingsDto.From(current));
            }

            Organization organization = await GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
            ApplyRowVersion(organization, rowVersion);

            buffer.Position = 0;
            string storedFileName = await logoStore.SaveAsync(buffer, inspection.Extension!, cancellationToken).ConfigureAwait(false);
            string? previousFileName = organization.Logo?.StoredFileName;

            try
            {
                organization.SetLogo(new OrganizationLogo(storedFileName, inspection.ContentType!, buffer.Length));
                OrganizationSettingsResult result = await SaveAsync(organization, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded && previousFileName is not null)
                {
                    await logoStore.DeleteAsync(previousFileName, cancellationToken).ConfigureAwait(false);
                }

                if (!result.Succeeded)
                {
                    await logoStore.DeleteAsync(storedFileName, cancellationToken).ConfigureAwait(false);
                }

                return result;
            }
            catch
            {
                await logoStore.DeleteAsync(storedFileName, cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
    }

    public async Task<OrganizationSettingsResult> RemoveLogoAsync(
        byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);

        OrganizationSettingsResult? forbidden = await ForbidIfUnauthorizedAsync().ConfigureAwait(false);
        if (forbidden is not null)
        {
            return forbidden;
        }

        Organization organization = await GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        ApplyRowVersion(organization, rowVersion);
        string? previousFileName = organization.Logo?.StoredFileName;
        organization.ClearLogo();

        OrganizationSettingsResult result = await SaveAsync(organization, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded && previousFileName is not null)
        {
            await logoStore.DeleteAsync(previousFileName, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<OrganizationSettingsResult?> ForbidIfUnauthorizedAsync()
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.ManageOrganizationSettings)
            .ConfigureAwait(false);

        return authorization.Succeeded ? null : OrganizationSettingsResult.Forbidden();
    }

    private async Task<Organization> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        Organization? organization = await dbContext.Organizations
            .FindAsync([Organization.SingletonId], cancellationToken)
            .ConfigureAwait(false);
        if (organization is not null)
        {
            return organization;
        }

        organization = Organization.CreateSingleton();
        dbContext.Organizations.Add(organization);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return organization;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(organization).State = EntityState.Detached;
            return await dbContext.Organizations
                .FindAsync([Organization.SingletonId], cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The organization profile could not be loaded.");
        }
    }

    private async Task<OrganizationSettingsResult> SaveAsync(Organization organization, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return OrganizationSettingsResult.Success(OrganizationSettingsDto.From(organization));
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbContext.Entry(organization).ReloadAsync(cancellationToken).ConfigureAwait(false);
            return OrganizationSettingsResult.ConcurrencyConflict(OrganizationSettingsDto.From(organization));
        }
    }

    private void ApplyRowVersion(Organization organization, byte[] rowVersion)
    {
        dbContext.Entry(organization).Property(entity => entity.RowVersion).OriginalValue = rowVersion;
    }

    private static async Task<MemoryStream> CopyLimitedAsync(Stream content, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > OrganizationLogoRules.MaxSizeBytes)
            {
                throw new InvalidDataException($"The logo must be {OrganizationLogoRules.SizeLimitDescription} or smaller.");
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }
}

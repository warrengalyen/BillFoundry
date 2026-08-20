using System.Globalization;
using BillFoundry.Application.Auditing;
using BillFoundry.Application.Invoices;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Documents;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Invoices;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Auditing;
using BillFoundry.Infrastructure.Documents;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BillFoundry.Infrastructure.Invoices;

internal sealed class InvoiceService(
    BillFoundryDbContext dbContext,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IAuditRecorder auditRecorder) : IInvoiceService
{
    public async Task<InvoiceListResult> ListAsync(InvoiceListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceListResult.Forbidden();
        }

        query.Normalize();
        DateOnly today = Today();
        IQueryable<Invoice> invoices = dbContext.Invoices.AsNoTracking();

        if (query.ClientId is Guid clientId)
        {
            invoices = invoices.Where(invoice => invoice.ClientId == clientId);
        }

        if (query.Search is not null)
        {
            string search = query.Search;
            invoices = invoices.Where(invoice =>
                invoice.Number.Contains(search)
                || invoice.ClientSnapshot.Name.Contains(search)
                || invoice.ClientSnapshot.Code.Contains(search)
                || (invoice.PurchaseOrder != null && invoice.PurchaseOrder.Contains(search))
                || (invoice.Notes != null && invoice.Notes.Contains(search)));
        }

        if (query.IssueFrom is DateOnly issueFrom)
        {
            invoices = invoices.Where(invoice => invoice.IssueDate >= issueFrom);
        }

        if (query.IssueTo is DateOnly issueTo)
        {
            invoices = invoices.Where(invoice => invoice.IssueDate <= issueTo);
        }

        if (query.DueFrom is DateOnly dueFrom)
        {
            invoices = invoices.Where(invoice => invoice.DueDate >= dueFrom);
        }

        if (query.DueTo is DateOnly dueTo)
        {
            invoices = invoices.Where(invoice => invoice.DueDate <= dueTo);
        }

        if (query.MinTotal is decimal minTotal)
        {
            invoices = invoices.Where(invoice => invoice.Total >= minTotal);
        }

        if (query.MaxTotal is decimal maxTotal)
        {
            invoices = invoices.Where(invoice => invoice.Total <= maxTotal);
        }

        invoices = ApplyStatusFilter(invoices, query, today);

        int totalCount = await invoices.CountAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<Invoice> sorted = query.SortBy switch
        {
            InvoiceSortField.Number when query.SortDescending =>
                invoices.OrderByDescending(invoice => invoice.Number).ThenByDescending(invoice => invoice.IssueDate),
            InvoiceSortField.Number =>
                invoices.OrderBy(invoice => invoice.Number).ThenBy(invoice => invoice.IssueDate),
            InvoiceSortField.Client when query.SortDescending =>
                invoices.OrderByDescending(invoice => invoice.ClientSnapshot.Name).ThenByDescending(invoice => invoice.Number),
            InvoiceSortField.Client =>
                invoices.OrderBy(invoice => invoice.ClientSnapshot.Name).ThenBy(invoice => invoice.Number),
            InvoiceSortField.DueDate when query.SortDescending =>
                invoices.OrderByDescending(invoice => invoice.DueDate).ThenByDescending(invoice => invoice.Number),
            InvoiceSortField.DueDate =>
                invoices.OrderBy(invoice => invoice.DueDate).ThenBy(invoice => invoice.Number),
            InvoiceSortField.Total when query.SortDescending =>
                invoices.OrderByDescending(invoice => invoice.Total).ThenByDescending(invoice => invoice.Number),
            InvoiceSortField.Total =>
                invoices.OrderBy(invoice => invoice.Total).ThenBy(invoice => invoice.Number),
            InvoiceSortField.BalanceDue when query.SortDescending =>
                invoices.OrderByDescending(invoice => invoice.BalanceDue).ThenByDescending(invoice => invoice.Number),
            InvoiceSortField.BalanceDue =>
                invoices.OrderBy(invoice => invoice.BalanceDue).ThenBy(invoice => invoice.Number),
            InvoiceSortField.Status when query.SortDescending =>
                invoices.OrderByDescending(invoice => invoice.Status).ThenByDescending(invoice => invoice.Number),
            InvoiceSortField.Status =>
                invoices.OrderBy(invoice => invoice.Status).ThenBy(invoice => invoice.Number),
            InvoiceSortField.CreatedAt when query.SortDescending =>
                invoices.OrderByDescending(invoice => invoice.CreatedAtUtc).ThenByDescending(invoice => invoice.Number),
            InvoiceSortField.CreatedAt =>
                invoices.OrderBy(invoice => invoice.CreatedAtUtc).ThenBy(invoice => invoice.Number),
            _ when query.SortDescending =>
                invoices.OrderByDescending(invoice => invoice.IssueDate).ThenByDescending(invoice => invoice.Number),
            _ =>
                invoices.OrderBy(invoice => invoice.IssueDate).ThenBy(invoice => invoice.Number)
        };

        List<Invoice> page = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return InvoiceListResult.Success(new PagedInvoiceResult<InvoiceListItemDto>
        {
            Items = [.. page.Select(invoice => ToListItem(invoice, today))],
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    public async Task<InvoiceResult> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        Invoice? invoice = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        return invoice is null
            ? InvoiceResult.NotFound()
            : InvoiceResult.Success(await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
    }

    public async Task<InvoiceOptionsResult> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceOptionsResult.Forbidden();
        }

        OrganizationSettings settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        List<InvoiceClientOption> clients = await dbContext.Clients.AsNoTracking()
            .Where(client => client.IsActive)
            .OrderBy(client => client.Name)
            .ThenBy(client => client.Code)
            .Select(client => new InvoiceClientOption
            {
                Id = client.Id,
                Name = client.Name,
                Code = client.Code
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<InvoiceCatalogOption> catalog = await dbContext.CatalogItems.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new InvoiceCatalogOption
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Unit = item.UnitType,
                UnitLabel = item.UnitType == CatalogUnitType.Hour ? "Hour"
                    : item.UnitType == CatalogUnitType.Day ? "Day"
                    : item.UnitType == CatalogUnitType.Item ? "Item"
                    : "Flat fee",
                UnitPrice = item.DefaultUnitPrice,
                IsTaxable = item.IsTaxable
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return InvoiceOptionsResult.Success(new InvoiceFormOptions
        {
            Clients = clients,
            CatalogItems = catalog,
            CurrencyCode = settings.Currency.Value,
            DefaultPaymentTermsDays = settings.PaymentTermsDays,
            DefaultNotes = settings.DefaultNotes,
            DefaultPaymentInstructions = settings.DefaultPaymentInstructions,
            Today = Today()
        });
    }

    public async Task<InvoiceResult> CreateAsync(SaveInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        IReadOnlyList<string> errors = InvoiceValidator.ValidateHeader(command, requireRowVersion: false);
        if (errors.Count > 0)
        {
            return InvoiceResult.Invalid(errors);
        }

        Client? client = await dbContext.Clients
            .FirstOrDefaultAsync(entity => entity.Id == command.ClientId, cancellationToken)
            .ConfigureAwait(false);
        if (client is null)
        {
            return InvoiceResult.Invalid(["The selected client was not found."]);
        }

        if (!client.IsActive)
        {
            return InvoiceResult.Invalid(["New invoices can only be created for active clients."]);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            OrganizationSettings settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            int sequence = await DocumentSequenceAllocator
                .AllocateAsync(dbContext, DocumentSequence.InvoiceKind, cancellationToken)
                .ConfigureAwait(false);
            Invoice invoice = Invoice.Create(
                sequence,
                InvoiceNumber.Format(settings.InvoicePrefix, sequence),
                command.ClientId,
                Snapshot(client),
                command.IssueDate,
                command.DueDate,
                command.PurchaseOrder,
                command.Notes,
                command.PaymentInstructions,
                command.Discount,
                command.TaxRatePercent,
                settings.Currency);

            dbContext.Invoices.Add(invoice);
            auditRecorder.Record(AuditWrites.Event(
                AuditActions.InvoiceCreated,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Created invoice {invoice.Number}."));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Success(await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Invalid([exception.Message]);
        }
        catch (DbUpdateException exception) when (UniqueConstraint.IsViolation(exception))
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Invalid(["An invoice with this number already exists. Try again."]);
        }
    }

    public async Task<InvoiceResult> UpdateHeaderAsync(UpdateInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        Invoice? invoice = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return InvoiceResult.NotFound();
        }

        IReadOnlyList<string> errors = InvoiceValidator.ValidateHeader(command, requireRowVersion: true);
        if (errors.Count > 0)
        {
            return InvoiceResult.Invalid(errors, await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }

        Client? client = await dbContext.Clients
            .FirstOrDefaultAsync(entity => entity.Id == command.ClientId, cancellationToken)
            .ConfigureAwait(false);
        if (client is null)
        {
            return InvoiceResult.Invalid(
                ["The selected client was not found."],
                await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }

        if (command.ClientId != invoice.ClientId && !client.IsActive)
        {
            return InvoiceResult.Invalid(
                ["Invoices can only be reassigned to an active client."],
                await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(invoice, command.RowVersion);
        try
        {
            invoice.UpdateHeader(
                command.ClientId,
                Snapshot(client),
                command.IssueDate,
                command.DueDate,
                command.PurchaseOrder,
                command.Notes,
                command.PaymentInstructions,
                command.Discount,
                command.TaxRatePercent);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return InvoiceResult.Invalid(
                [exception.Message],
                await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }

        auditRecorder.Record(AuditWrites.Event(
            AuditActions.InvoiceUpdated,
            AuditEntityTypes.Invoice,
            invoice.Id,
            $"Updated invoice {invoice.Number}."));
        return await SaveAsync(invoice, cancellationToken).ConfigureAwait(false);
    }

    public async Task<InvoiceResult> AddLineAsync(SaveInvoiceLineCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        var errors = InvoiceValidator.ValidateLine(command).ToList();
        if (command.CatalogItemId is Guid catalogItemId
            && errors.Count == 0
            && !await dbContext.CatalogItems.AnyAsync(item => item.Id == catalogItemId, cancellationToken).ConfigureAwait(false))
        {
            errors.Add("The selected catalog item was not found.");
        }

        return await MutateAsync(
            command,
            errors,
            invoice => invoice.AddLine(
                command.CatalogItemId,
                command.Description,
                command.Quantity,
                command.Unit,
                command.UnitPrice,
                command.IsTaxable),
            invoice => AuditWrites.Event(
                AuditActions.InvoiceUpdated,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Updated invoice {invoice.Number}."),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<InvoiceResult> UpdateLineAsync(UpdateInvoiceLineCommand command, CancellationToken cancellationToken = default) =>
        MutateAsync(command, InvoiceValidator.ValidateLine(command), invoice =>
        {
            invoice.UpdateLine(
                command.LineId,
                command.Description,
                command.Quantity,
                command.Unit,
                command.UnitPrice,
                command.IsTaxable);
        }, cancellationToken);

    public Task<InvoiceResult> RemoveLineAsync(RemoveInvoiceLineCommand command, CancellationToken cancellationToken = default) =>
        MutateAsync(command, InvoiceValidator.ValidateConcurrency(command), invoice =>
        {
            invoice.RemoveLine(command.LineId);
        }, cancellationToken);

    public async Task<InvoiceResult> ReorderLinesAsync(
        ReorderInvoiceLinesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        Invoice? invoice = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return InvoiceResult.NotFound();
        }

        IReadOnlyList<string> errors = InvoiceValidator.ValidateConcurrency(command);
        if (errors.Count > 0)
        {
            return InvoiceResult.Invalid(errors, await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(invoice, command.RowVersion);
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            invoice.StageLineReorder(command.LineIds);
            Touch(invoice);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            invoice.ReorderLines(command.LineIds);
            Touch(invoice);
            auditRecorder.Record(AuditWrites.Event(
                AuditActions.InvoiceUpdated,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Updated invoice {invoice.Number}."));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Success(await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Invalid(
                [UserFacingMessage(exception)],
                await ReloadDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.ConcurrencyConflict(await ReloadDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<InvoiceResult> DuplicateAsync(DuplicateInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        Invoice? source = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return InvoiceResult.NotFound();
        }

        Client? client = await dbContext.Clients
            .FirstOrDefaultAsync(entity => entity.Id == source.ClientId, cancellationToken)
            .ConfigureAwait(false);
        if (client is null || !client.IsActive)
        {
            return InvoiceResult.Invalid(
                ["A duplicate can only be created when the original client is still active."],
                await ToDetailsAsync(source, cancellationToken).ConfigureAwait(false));
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            OrganizationSettings settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            int sequence = await DocumentSequenceAllocator
                .AllocateAsync(dbContext, DocumentSequence.InvoiceKind, cancellationToken)
                .ConfigureAwait(false);
            DateOnly issueDate = Today();
            DateOnly dueDate = settings.PaymentTermsDays > 0
                ? issueDate.AddDays(settings.PaymentTermsDays)
                : issueDate.AddDays(source.DueDate.DayNumber - source.IssueDate.DayNumber);

            Invoice copy = source.Duplicate(
                sequence,
                InvoiceNumber.Format(settings.InvoicePrefix, sequence),
                issueDate,
                dueDate);
            dbContext.Invoices.Add(copy);
            auditRecorder.Record(AuditWrites.Event(
                AuditActions.InvoiceDuplicated,
                AuditEntityTypes.Invoice,
                copy.Id,
                $"Created invoice {copy.Number} as a copy of {source.Number}.",
                new Dictionary<string, string?> { ["sourceInvoiceId"] = source.Id.ToString() }));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Success(await ToDetailsAsync(copy, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Invalid([exception.Message]);
        }
        catch (DbUpdateException exception) when (UniqueConstraint.IsViolation(exception))
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Invalid(["An invoice with this number already exists. Try again."]);
        }
    }

    public Task<InvoiceResult> MarkSentAsync(InvoiceConcurrencyCommand command, CancellationToken cancellationToken = default) =>
        MutateAsync(
            command,
            InvoiceValidator.ValidateConcurrency(command),
            invoice => invoice.MarkSent(),
            invoice => AuditWrites.Event(
                AuditActions.InvoiceSent,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Marked invoice {invoice.Number} as sent."),
            cancellationToken);

    public Task<InvoiceResult> VoidAsync(VoidInvoiceCommand command, CancellationToken cancellationToken = default) =>
        MutateAsync(
            command,
            InvoiceValidator.ValidateVoid(command),
            invoice => invoice.Void(command.Reason),
            invoice => AuditWrites.Event(
                AuditActions.InvoiceVoided,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Voided invoice {invoice.Number}."),
            cancellationToken);

    public async Task<InvoiceResult> RecordPaymentAsync(
        RecordPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        Invoice? invoice = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return InvoiceResult.NotFound();
        }

        IReadOnlyList<string> errors = InvoiceValidator.ValidatePayment(command);
        if (errors.Count > 0)
        {
            return InvoiceResult.Invalid(errors, await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(invoice, command.RowVersion);
        try
        {
            invoice.RecordPayment(
                Today(),
                command.PaymentDate,
                command.Amount,
                command.Method,
                command.Reference,
                command.Notes);
            Touch(invoice);
            auditRecorder.Record(AuditWrites.Event(
                AuditActions.PaymentRecorded,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Recorded a {AuditWrites.Money(command.Amount, invoice.Currency.Value)} payment on invoice {invoice.Number}.",
                new Dictionary<string, string?>
                {
                    ["amount"] = command.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                    ["method"] = PaymentMethodDisplay.Label(command.Method)
                }));
            return await SaveAsync(invoice, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return InvoiceResult.Invalid(
                [UserFacingMessage(exception)],
                await ReloadDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<InvoiceResult> ReversePaymentAsync(
        ReversePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        Invoice? invoice = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return InvoiceResult.NotFound();
        }

        IReadOnlyList<string> errors = InvoiceValidator.ValidateReverse(command);
        if (errors.Count > 0)
        {
            return InvoiceResult.Invalid(errors, await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(invoice, command.RowVersion);
        try
        {
            invoice.ReversePayment(command.PaymentId, Today(), command.Reason);
            Touch(invoice);
            auditRecorder.Record(AuditWrites.Event(
                AuditActions.PaymentReversed,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Reversed a payment on invoice {invoice.Number}.",
                new Dictionary<string, string?> { ["paymentId"] = command.PaymentId.ToString() }));
            return await SaveAsync(invoice, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return InvoiceResult.Invalid(
                [UserFacingMessage(exception)],
                await ReloadDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<InvoiceResult> ConvertFromEstimateAsync(
        ConvertEstimateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        IReadOnlyList<string> errors = InvoiceValidator.ValidateConvert(command);
        if (errors.Count > 0)
        {
            return InvoiceResult.Invalid(errors);
        }

        Estimate? estimate = await dbContext.Estimates
            .Include(entity => entity.Lines)
            .FirstOrDefaultAsync(entity => entity.Id == command.EstimateId, cancellationToken)
            .ConfigureAwait(false);
        if (estimate is null)
        {
            return InvoiceResult.Invalid(["The estimate was not found."]);
        }

        if (estimate.Status is EstimateStatus.Converted
            || await dbContext.Invoices.AnyAsync(invoice => invoice.SourceEstimateId == estimate.Id, cancellationToken)
                .ConfigureAwait(false))
        {
            return InvoiceResult.Invalid(["This estimate has already been converted to an invoice."]);
        }

        if (estimate.Status is not EstimateStatus.Accepted)
        {
            return InvoiceResult.Invalid(["Only an accepted estimate can be converted to an invoice."]);
        }

        Client? client = await dbContext.Clients
            .FirstOrDefaultAsync(entity => entity.Id == estimate.ClientId, cancellationToken)
            .ConfigureAwait(false);
        if (client is null)
        {
            return InvoiceResult.Invalid(["The estimate client was not found."]);
        }

        dbContext.Entry(estimate).Property(entity => entity.RowVersion).OriginalValue = command.EstimateRowVersion;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            OrganizationSettings settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            int sequence = await DocumentSequenceAllocator
                .AllocateAsync(dbContext, DocumentSequence.InvoiceKind, cancellationToken)
                .ConfigureAwait(false);
            DateOnly issueDate = command.IssueDate ?? Today();
            DateOnly dueDate = command.DueDate
                ?? (settings.PaymentTermsDays > 0 ? issueDate.AddDays(settings.PaymentTermsDays) : issueDate);

            Invoice invoice = Invoice.FromEstimate(
                estimate,
                Snapshot(client),
                sequence,
                InvoiceNumber.Format(settings.InvoicePrefix, sequence),
                issueDate,
                dueDate,
                command.PurchaseOrder,
                command.Notes,
                command.PaymentInstructions ?? settings.DefaultPaymentInstructions);

            estimate.TransitionTo(EstimateStatus.Converted);
            TouchEstimate(estimate);
            dbContext.Invoices.Add(invoice);
            auditRecorder.Record(AuditWrites.Event(
                AuditActions.InvoiceConvertedFromEstimate,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Created invoice {invoice.Number} from estimate {estimate.Number}.",
                new Dictionary<string, string?> { ["estimateId"] = estimate.Id.ToString() }));
            auditRecorder.Record(AuditWrites.Event(
                AuditActions.EstimateStatusChanged,
                AuditEntityTypes.Estimate,
                estimate.Id,
                $"Changed estimate {estimate.Number} to {EstimateStatusRules.Label(estimate.Status)}."));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Success(await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Invalid(["The estimate was updated by another user. Reload it and try again."]);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Invalid([exception.Message]);
        }
        catch (DbUpdateException exception) when (UniqueConstraint.IsViolation(exception))
        {
            await RollbackTrackedAsync(transaction, cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Invalid(["This estimate has already been converted to an invoice."]);
        }
    }

    private async Task<InvoiceResult> MutateAsync(
        InvoiceConcurrencyCommand command,
        IReadOnlyList<string> errors,
        Action<Invoice> mutate,
        CancellationToken cancellationToken) =>
        await MutateAsync(
            command,
            errors,
            mutate,
            invoice => AuditWrites.Event(
                AuditActions.InvoiceUpdated,
                AuditEntityTypes.Invoice,
                invoice.Id,
                $"Updated invoice {invoice.Number}."),
            cancellationToken).ConfigureAwait(false);

    private async Task<InvoiceResult> MutateAsync(
        InvoiceConcurrencyCommand command,
        IReadOnlyList<string> errors,
        Action<Invoice> mutate,
        Func<Invoice, AuditWriteRequest> audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return InvoiceResult.Forbidden();
        }

        Invoice? invoice = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return InvoiceResult.NotFound();
        }

        if (errors.Count > 0)
        {
            return InvoiceResult.Invalid(errors, await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(invoice, command.RowVersion);
        try
        {
            mutate(invoice);
            Touch(invoice);
            auditRecorder.Record(audit(invoice));
            return await SaveAsync(invoice, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return InvoiceResult.Invalid(
                [UserFacingMessage(exception)],
                await ReloadDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
    }

    private async Task<bool> IsForbiddenAsync()
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.ManageInvoices)
            .ConfigureAwait(false);
        return !authorization.Succeeded;
    }

    private Task<Invoice?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Invoices
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.Payments)
            .FirstOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);

    private async Task<OrganizationSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        Organization? organization = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == Organization.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        return organization is null
            ? new OrganizationSettings(
                CurrencyCode.Usd,
                DocumentPrefix.InvoiceDefault,
                0,
                null,
                null)
            : new OrganizationSettings(
                organization.DefaultCurrency,
                organization.DefaultInvoicePrefix,
                organization.DefaultPaymentTermsDays,
                organization.DefaultInvoiceNotes,
                organization.DefaultPaymentInstructions);
    }

    private async Task RollbackTrackedAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private void ApplyRowVersion(Invoice invoice, byte[] rowVersion)
    {
        dbContext.Entry(invoice).Property(entity => entity.RowVersion).OriginalValue = rowVersion;
    }

    private void Touch(Invoice invoice)
    {
        invoice.SetUpdated(timeProvider.GetUtcNow(), currentUser.UserId);
        dbContext.Entry(invoice).Property(entity => entity.UpdatedAtUtc).IsModified = true;
        dbContext.Entry(invoice).Property(entity => entity.UpdatedByUserId).IsModified = true;
    }

    private void TouchEstimate(Estimate estimate)
    {
        estimate.SetUpdated(timeProvider.GetUtcNow(), currentUser.UserId);
        dbContext.Entry(estimate).Property(entity => entity.UpdatedAtUtc).IsModified = true;
        dbContext.Entry(estimate).Property(entity => entity.UpdatedByUserId).IsModified = true;
    }

    private async Task<InvoiceDetailsDto> ToDetailsAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        byte[] rowVersion = dbContext.Entry(invoice).Property(entity => entity.RowVersion).CurrentValue
            ?? invoice.RowVersion;
        bool clientIsActive = await dbContext.Clients.AsNoTracking()
            .Where(entity => entity.Id == invoice.ClientId)
            .Select(entity => entity.IsActive)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        return InvoiceDetailsDto.From(invoice, clientIsActive, Today(), rowVersion);
    }

    private async Task<InvoiceDetailsDto> ReloadDetailsAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Invoice current = await LoadAsync(invoice.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The invoice could not be reloaded.");
        return await ToDetailsAsync(current, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InvoiceResult> SaveAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return InvoiceResult.Success(await ToDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateConcurrencyException)
        {
            AuditChangeTracker.DiscardPending(dbContext);
            return InvoiceResult.ConcurrencyConflict(await ReloadDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateException exception) when (UniqueConstraint.IsViolation(exception))
        {
            AuditChangeTracker.DiscardPending(dbContext);
            return InvoiceResult.Invalid(
                ["The invoice could not be saved because a uniqueness constraint was violated."],
                await ReloadDetailsAsync(invoice, cancellationToken).ConfigureAwait(false));
        }
    }

    private static IQueryable<Invoice> ApplyStatusFilter(IQueryable<Invoice> invoices, InvoiceListQuery query, DateOnly today)
    {
        if (query.Status is InvoiceStatusFilter.Overdue || query.OverdueOnly)
        {
            return invoices.Where(invoice =>
                (invoice.Status == InvoiceStatus.Sent || invoice.Status == InvoiceStatus.PartiallyPaid)
                && invoice.DueDate < today
                && invoice.BalanceDue > 0m);
        }

        return query.Status switch
        {
            InvoiceStatusFilter.All => invoices,
            InvoiceStatusFilter.Sent => invoices.Where(invoice =>
                invoice.Status == InvoiceStatus.Sent
                && (invoice.DueDate >= today || invoice.BalanceDue <= 0m)),
            InvoiceStatusFilter.Draft => invoices.Where(invoice => invoice.Status == InvoiceStatus.Draft),
            InvoiceStatusFilter.PartiallyPaid => invoices.Where(invoice => invoice.Status == InvoiceStatus.PartiallyPaid),
            InvoiceStatusFilter.Paid => invoices.Where(invoice => invoice.Status == InvoiceStatus.Paid),
            InvoiceStatusFilter.Void => invoices.Where(invoice => invoice.Status == InvoiceStatus.Void),
            _ => invoices
        };
    }

    private static InvoiceListItemDto ToListItem(Invoice invoice, DateOnly today)
    {
        InvoiceStatus effective = invoice.EffectiveStatus(today);
        return new InvoiceListItemDto
        {
            Id = invoice.Id,
            Number = invoice.Number,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientSnapshot.Name,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            Status = invoice.Status,
            EffectiveStatus = effective,
            StatusLabel = InvoiceStatusRules.Label(effective),
            Total = invoice.Total,
            BalanceDue = invoice.BalanceDue,
            CurrencyCode = invoice.Currency.Value,
            CreatedAtUtc = invoice.CreatedAtUtc
        };
    }

    private static InvoiceClientSnapshot Snapshot(Client client) =>
        InvoiceClientSnapshot.Capture(client.Name, client.Code, client.Email);

    private static string UserFacingMessage(Exception exception) =>
        exception.Message.StartsWith("Discount cannot exceed", StringComparison.Ordinal)
            ? "Discount cannot exceed the subtotal."
            : exception.Message;

    private readonly record struct OrganizationSettings(
        CurrencyCode Currency,
        DocumentPrefix InvoicePrefix,
        int PaymentTermsDays,
        string? DefaultNotes,
        string? DefaultPaymentInstructions);
}

using BillFoundry.Application.Estimates;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Documents;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Estimates;

internal sealed class EstimateService(
    BillFoundryDbContext dbContext,
    IAuthorizationService authorizationService,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IEstimateService
{
    public async Task<EstimateListResult> ListAsync(EstimateListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateListResult.Forbidden();
        }

        query.Normalize();

        IQueryable<EstimateListRow> rows =
            from estimate in dbContext.Estimates.AsNoTracking()
            join client in dbContext.Clients.AsNoTracking() on estimate.ClientId equals client.Id
            select new EstimateListRow
            {
                Id = estimate.Id,
                Number = estimate.Number,
                ClientId = estimate.ClientId,
                ClientName = client.Name,
                IssueDate = estimate.IssueDate,
                ExpirationDate = estimate.ExpirationDate,
                Status = estimate.Status,
                Total = estimate.Total,
                CurrencyCode = estimate.Currency,
                CreatedAtUtc = estimate.CreatedAtUtc,
                Notes = estimate.Notes
            };

        EstimateStatus? status = query.StatusValue();
        if (status is EstimateStatus filtered)
        {
            rows = rows.Where(row => row.Status == filtered);
        }

        if (query.Search is not null)
        {
            string search = query.Search;
            rows = rows.Where(row =>
                row.Number.Contains(search)
                || row.ClientName.Contains(search)
                || (row.Notes != null && row.Notes.Contains(search)));
        }

        int totalCount = await rows.CountAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<EstimateListRow> sorted = query.SortBy switch
        {
            EstimateSortField.Number when query.SortDescending =>
                rows.OrderByDescending(row => row.Number).ThenByDescending(row => row.IssueDate),
            EstimateSortField.Number =>
                rows.OrderBy(row => row.Number).ThenBy(row => row.IssueDate),
            EstimateSortField.Client when query.SortDescending =>
                rows.OrderByDescending(row => row.ClientName).ThenByDescending(row => row.Number),
            EstimateSortField.Client =>
                rows.OrderBy(row => row.ClientName).ThenBy(row => row.Number),
            EstimateSortField.Total when query.SortDescending =>
                rows.OrderByDescending(row => row.Total).ThenByDescending(row => row.Number),
            EstimateSortField.Total =>
                rows.OrderBy(row => row.Total).ThenBy(row => row.Number),
            EstimateSortField.Status when query.SortDescending =>
                rows.OrderByDescending(row => row.Status).ThenByDescending(row => row.Number),
            EstimateSortField.Status =>
                rows.OrderBy(row => row.Status).ThenBy(row => row.Number),
            EstimateSortField.CreatedAt when query.SortDescending =>
                rows.OrderByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Number),
            EstimateSortField.CreatedAt =>
                rows.OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Number),
            _ when query.SortDescending =>
                rows.OrderByDescending(row => row.IssueDate).ThenByDescending(row => row.Number),
            _ =>
                rows.OrderBy(row => row.IssueDate).ThenBy(row => row.Number)
        };

        List<EstimateListRow> page = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return EstimateListResult.Success(new PagedEstimateResult<EstimateListItemDto>
        {
            Items = [.. page.Select(row => row.ToDto())],
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    public async Task<EstimateResult> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateResult.Forbidden();
        }

        Estimate? estimate = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        return estimate is null
            ? EstimateResult.NotFound()
            : EstimateResult.Success(await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
    }

    public async Task<EstimateOptionsResult> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateOptionsResult.Forbidden();
        }

        OrganizationSettings settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        List<EstimateClientOption> clients = await dbContext.Clients.AsNoTracking()
            .Where(client => client.IsActive)
            .OrderBy(client => client.Name)
            .ThenBy(client => client.Code)
            .Select(client => new EstimateClientOption
            {
                Id = client.Id,
                Name = client.Name,
                Code = client.Code
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<EstimateCatalogOption> catalog = await dbContext.CatalogItems.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new EstimateCatalogOption
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

        return EstimateOptionsResult.Success(new EstimateFormOptions
        {
            Clients = clients,
            CatalogItems = catalog,
            CurrencyCode = settings.Currency.Value,
            DefaultPaymentTermsDays = settings.PaymentTermsDays,
            DefaultNotes = settings.DefaultNotes,
            Today = Today()
        });
    }

    public async Task<EstimateResult> CreateAsync(SaveEstimateCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateResult.Forbidden();
        }

        IReadOnlyList<string> errors = EstimateValidator.ValidateHeader(command, requireRowVersion: false);
        if (errors.Count > 0)
        {
            return EstimateResult.Invalid(errors);
        }

        Client? client = await dbContext.Clients
            .FirstOrDefaultAsync(entity => entity.Id == command.ClientId, cancellationToken)
            .ConfigureAwait(false);
        if (client is null)
        {
            return EstimateResult.Invalid(["The selected client was not found."]);
        }

        if (!client.IsActive)
        {
            return EstimateResult.Invalid(["New estimates can only be created for active clients."]);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            OrganizationSettings settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            int sequence = await AllocateEstimateSequenceAsync(cancellationToken).ConfigureAwait(false);
            EstimateNumber number = EstimateNumber.Format(settings.EstimatePrefix, sequence);
            Estimate estimate = Estimate.Create(
                sequence,
                number,
                command.ClientId,
                command.IssueDate,
                command.ExpirationDate,
                command.Notes,
                command.Terms,
                command.Discount,
                command.TaxRatePercent,
                settings.Currency);

            dbContext.Estimates.Add(estimate);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Success(await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Invalid([exception.Message]);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Invalid([exception.Message]);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Invalid(["An estimate with this number already exists. Try again."]);
        }
    }

    public async Task<EstimateResult> UpdateHeaderAsync(UpdateEstimateCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateResult.Forbidden();
        }

        Estimate? estimate = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (estimate is null)
        {
            return EstimateResult.NotFound();
        }

        IReadOnlyList<string> errors = EstimateValidator.ValidateHeader(command, requireRowVersion: true);
        if (errors.Count > 0)
        {
            return EstimateResult.Invalid(errors, await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }

        Client? client = await dbContext.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == command.ClientId, cancellationToken)
            .ConfigureAwait(false);
        if (client is null)
        {
            return EstimateResult.Invalid(
                ["The selected client was not found."],
                await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }

        if (command.ClientId != estimate.ClientId && !client.IsActive)
        {
            return EstimateResult.Invalid(
                ["Estimates can only be reassigned to an active client."],
                await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(estimate, command.RowVersion);
        try
        {
            estimate.UpdateHeader(
                command.ClientId,
                command.IssueDate,
                command.ExpirationDate,
                command.Notes,
                command.Terms,
                command.Discount,
                command.TaxRatePercent);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return EstimateResult.Invalid(
                [exception.Message],
                await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }

        return await SaveAsync(estimate, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EstimateResult> AddLineAsync(SaveEstimateLineCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateResult.Forbidden();
        }

        var errors = EstimateValidator.ValidateLine(command).ToList();
        if (command.CatalogItemId is Guid catalogItemId
            && errors.Count == 0
            && !await dbContext.CatalogItems.AnyAsync(item => item.Id == catalogItemId, cancellationToken).ConfigureAwait(false))
        {
            errors.Add("The selected catalog item was not found.");
        }

        return await MutateAsync(
            command,
            errors,
            estimate => estimate.AddLine(
                command.CatalogItemId,
                command.Description,
                command.Quantity,
                command.Unit,
                command.UnitPrice,
                command.IsTaxable),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<EstimateResult> UpdateLineAsync(UpdateEstimateLineCommand command, CancellationToken cancellationToken = default) =>
        MutateAsync(command, EstimateValidator.ValidateLine(command), estimate =>
        {
            estimate.UpdateLine(
                command.LineId,
                command.Description,
                command.Quantity,
                command.Unit,
                command.UnitPrice,
                command.IsTaxable);
        }, cancellationToken);

    public Task<EstimateResult> RemoveLineAsync(RemoveEstimateLineCommand command, CancellationToken cancellationToken = default) =>
        MutateAsync(command, EstimateValidator.ValidateConcurrency(command), estimate =>
        {
            estimate.RemoveLine(command.LineId);
        }, cancellationToken);

    public async Task<EstimateResult> ReorderLinesAsync(
        ReorderEstimateLinesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateResult.Forbidden();
        }

        Estimate? estimate = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (estimate is null)
        {
            return EstimateResult.NotFound();
        }

        IReadOnlyList<string> errors = EstimateValidator.ValidateConcurrency(command);
        if (errors.Count > 0)
        {
            return EstimateResult.Invalid(errors, await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(estimate, command.RowVersion);
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            estimate.StageLineReorder(command.LineIds);
            Touch(estimate);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            estimate.ReorderLines(command.LineIds);
            Touch(estimate);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Success(await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Invalid(
                [UserFacingMessage(exception)],
                await ReloadDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.ConcurrencyConflict(await ReloadDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<EstimateResult> DuplicateAsync(DuplicateEstimateCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateResult.Forbidden();
        }

        Estimate? source = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return EstimateResult.NotFound();
        }

        Client? client = await dbContext.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == source.ClientId, cancellationToken)
            .ConfigureAwait(false);
        if (client is null || !client.IsActive)
        {
            return EstimateResult.Invalid(
                ["A duplicate can only be created when the original client is still active."],
                await ToDetailsAsync(source, cancellationToken).ConfigureAwait(false));
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            OrganizationSettings settings = await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            int sequence = await AllocateEstimateSequenceAsync(cancellationToken).ConfigureAwait(false);
            EstimateNumber number = EstimateNumber.Format(settings.EstimatePrefix, sequence);
            DateOnly issueDate = Today();
            DateOnly? expiration = settings.PaymentTermsDays > 0
                ? issueDate.AddDays(settings.PaymentTermsDays)
                : source.ExpirationDate is DateOnly originalExpiration
                    ? issueDate.AddDays(originalExpiration.DayNumber - source.IssueDate.DayNumber)
                    : null;

            Estimate copy = source.Duplicate(sequence, number, issueDate, expiration);
            dbContext.Estimates.Add(copy);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Success(await ToDetailsAsync(copy, cancellationToken).ConfigureAwait(false));
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Invalid([exception.Message]);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Invalid([exception.Message]);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Invalid(["An estimate with this number already exists. Try again."]);
        }
    }

    public Task<EstimateResult> TransitionAsync(TransitionEstimateCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Target is EstimateStatus.Converted)
        {
            return MutateAsync(
                command,
                ["Estimate-to-invoice conversion is not available yet."],
                _ => { },
                cancellationToken);
        }

        if (!EstimateStatusRules.IsDefined(command.Target))
        {
            return MutateAsync(command, ["The estimate status is not valid."], _ => { }, cancellationToken);
        }

        return MutateAsync(
            command,
            EstimateValidator.ValidateConcurrency(command),
            estimate => estimate.TransitionTo(command.Target),
            cancellationToken);
    }

    private async Task<EstimateResult> MutateAsync(
        EstimateConcurrencyCommand command,
        IReadOnlyList<string> errors,
        Action<Estimate> mutate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await IsForbiddenAsync().ConfigureAwait(false))
        {
            return EstimateResult.Forbidden();
        }

        Estimate? estimate = await LoadAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (estimate is null)
        {
            return EstimateResult.NotFound();
        }

        if (errors.Count > 0)
        {
            return EstimateResult.Invalid(errors, await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }

        ApplyRowVersion(estimate, command.RowVersion);
        try
        {
            mutate(estimate);
            Touch(estimate);
            return await SaveAsync(estimate, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return EstimateResult.Invalid(
                [UserFacingMessage(exception)],
                await ReloadDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }
    }

    private async Task<bool> IsForbiddenAsync()
    {
        AuthorizationResult authorization = await authorizationService
            .AuthorizeAsync(currentUser.Principal, AuthorizationPolicies.ManageEstimates)
            .ConfigureAwait(false);
        return !authorization.Succeeded;
    }

    private Task<Estimate?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Estimates
            .Include(estimate => estimate.Lines)
            .FirstOrDefaultAsync(estimate => estimate.Id == id, cancellationToken);

    private async Task<int> AllocateEstimateSequenceAsync(CancellationToken cancellationToken)
    {
        DocumentSequence sequence = await dbContext.DocumentSequences
            .FromSql($"SELECT [Kind], [NextValue] FROM [DocumentSequences] WITH (UPDLOCK, HOLDLOCK) WHERE [Kind] = {DocumentSequence.EstimateKind}")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return sequence.Allocate();
    }

    private async Task<OrganizationSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        Organization? organization = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == Organization.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        return organization is null
            ? new OrganizationSettings(CurrencyCode.Usd, DocumentPrefix.EstimateDefault, 0, null)
            : new OrganizationSettings(
                organization.DefaultCurrency,
                organization.DefaultEstimatePrefix,
                organization.DefaultPaymentTermsDays,
                organization.DefaultInvoiceNotes);
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private void ApplyRowVersion(Estimate estimate, byte[] rowVersion)
    {
        dbContext.Entry(estimate).Property(entity => entity.RowVersion).OriginalValue = rowVersion;
    }

    private void Touch(Estimate estimate)
    {
        estimate.SetUpdated(timeProvider.GetUtcNow(), currentUser.UserId);
        dbContext.Entry(estimate).Property(entity => entity.UpdatedAtUtc).IsModified = true;
        dbContext.Entry(estimate).Property(entity => entity.UpdatedByUserId).IsModified = true;
    }

    private async Task<EstimateDetailsDto> ToDetailsAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        byte[] rowVersion = dbContext.Entry(estimate).Property(entity => entity.RowVersion).CurrentValue
            ?? estimate.RowVersion;
        var client = await dbContext.Clients.AsNoTracking()
            .Where(entity => entity.Id == estimate.ClientId)
            .Select(entity => new { entity.Name, entity.IsActive })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        return EstimateDetailsDto.From(estimate, client.Name, client.IsActive, rowVersion);
    }

    private async Task<EstimateDetailsDto> ReloadDetailsAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Estimate current = await LoadAsync(estimate.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The estimate could not be reloaded.");
        return await ToDetailsAsync(current, cancellationToken).ConfigureAwait(false);
    }

    private async Task<EstimateResult> SaveAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return EstimateResult.Success(await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateConcurrencyException)
        {
            return EstimateResult.ConcurrencyConflict(await ReloadDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return EstimateResult.Invalid(
                ["The estimate could not be saved because a uniqueness constraint was violated."],
                await ToDetailsAsync(estimate, cancellationToken).ConfigureAwait(false));
        }
    }

    private static string UserFacingMessage(Exception exception) =>
        exception.Message.StartsWith("Discount cannot exceed", StringComparison.Ordinal)
            ? "Discount cannot exceed the subtotal."
            : exception.Message;

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Number is 2601 or 2627;

    private readonly record struct OrganizationSettings(
        CurrencyCode Currency,
        DocumentPrefix EstimatePrefix,
        int PaymentTermsDays,
        string? DefaultNotes);

    private sealed class EstimateListRow
    {
        public Guid Id { get; init; }

        public string Number { get; init; } = string.Empty;

        public Guid ClientId { get; init; }

        public string ClientName { get; init; } = string.Empty;

        public DateOnly IssueDate { get; init; }

        public DateOnly? ExpirationDate { get; init; }

        public EstimateStatus Status { get; init; }

        public decimal Total { get; init; }

        public CurrencyCode CurrencyCode { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public string? Notes { get; init; }

        public EstimateListItemDto ToDto() => new()
        {
            Id = Id,
            Number = Number,
            ClientId = ClientId,
            ClientName = ClientName,
            IssueDate = IssueDate,
            ExpirationDate = ExpirationDate,
            Status = Status,
            StatusLabel = EstimateStatusRules.Label(Status),
            Total = Total,
            CurrencyCode = CurrencyCode.Value,
            CreatedAtUtc = CreatedAtUtc
        };
    }
}

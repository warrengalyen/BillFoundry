namespace BillFoundry.Application.Auditing;

/// <summary>
/// Writes an append-only business audit row onto the current EF Core context.
/// Callers persist it with their own <c>SaveChanges</c> so financial work and
/// the audit row share a transaction.
/// </summary>
public interface IAuditRecorder
{
    void Record(AuditWriteRequest request);

    Task PersistAsync(CancellationToken cancellationToken = default);
}

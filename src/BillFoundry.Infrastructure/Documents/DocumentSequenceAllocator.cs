using BillFoundry.Domain.Documents;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Documents;

internal static class DocumentSequenceAllocator
{
    public static async Task<int> AllocateAsync(
        BillFoundryDbContext dbContext,
        string kind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        DocumentSequence sequence = await dbContext.DocumentSequences
            .FromSql($"SELECT [Kind], [NextValue] FROM [DocumentSequences] WITH (UPDLOCK, HOLDLOCK) WHERE [Kind] = {kind}")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return sequence.Allocate();
    }
}

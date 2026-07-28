using BillFoundry.Domain.Documents;

namespace BillFoundry.Domain.Tests;

public sealed class DocumentSequenceTests
{
    [Fact]
    public void Allocate_returns_the_current_value_then_increments()
    {
        DocumentSequence sequence = DocumentSequence.Create(DocumentSequence.EstimateKind);

        Assert.Equal(1, sequence.Allocate());
        Assert.Equal(2, sequence.Allocate());
        Assert.Equal(3, sequence.NextValue);
    }
}

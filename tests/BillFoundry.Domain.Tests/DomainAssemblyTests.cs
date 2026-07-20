using System.Reflection;

namespace BillFoundry.Domain.Tests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void Domain_assembly_can_be_loaded()
    {
        var assembly = Assembly.Load("BillFoundry.Domain");

        Assert.NotNull(assembly);
        Assert.Equal("BillFoundry.Domain", assembly.GetName().Name);
    }
}

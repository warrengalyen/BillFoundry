using BillFoundry.Application.Documents;

namespace BillFoundry.Application.Tests;

public sealed class DocumentFileNameTests
{
    [Fact]
    public void ForInvoice_uses_the_sanitized_number()
    {
        Assert.Equal("invoice-INV-0001.pdf", DocumentFileName.ForInvoice("INV-0001"));
    }

    [Fact]
    public void ForEstimate_uses_the_sanitized_number()
    {
        Assert.Equal("estimate-EST-0001.pdf", DocumentFileName.ForEstimate("EST-0001"));
    }

    [Fact]
    public void ForInvoice_strips_path_characters()
    {
        string name = DocumentFileName.ForInvoice(@"..\INV/0001");

        Assert.Equal("invoice-INV-0001.pdf", name);
        Assert.Equal(name, Path.GetFileName(name));
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain('\\', name);
        Assert.DoesNotContain("..", name, StringComparison.Ordinal);
    }

    [Fact]
    public void ForInvoice_falls_back_when_the_number_is_empty()
    {
        Assert.Equal("invoice.pdf", DocumentFileName.ForInvoice("   "));
        Assert.Equal("invoice.pdf", DocumentFileName.ForInvoice(null));
    }

    [Fact]
    public void ForEstimate_falls_back_when_the_number_is_empty()
    {
        Assert.Equal("estimate.pdf", DocumentFileName.ForEstimate(null));
    }

    [Fact]
    public void File_names_stay_within_the_maximum_length()
    {
        string name = DocumentFileName.ForInvoice(new string('A', 200));

        Assert.True(name.Length <= DocumentFileName.MaxLength);
        Assert.EndsWith(".pdf", name, StringComparison.Ordinal);
        Assert.StartsWith("invoice-", name, StringComparison.Ordinal);
    }
}

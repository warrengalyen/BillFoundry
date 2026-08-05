using System.Text;
using BillFoundry.Application.Reporting;

namespace BillFoundry.Application.Tests;

public sealed class CsvFormatterTests
{
    [Fact]
    public void Quotes_commas_and_doubles_quotes()
    {
        byte[] bytes = CsvFormatter.ToUtf8(
            ["Name", "Note"],
            [["Acme, LLC", "He said \"paid\""]]);
        string csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"Acme, LLC\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"He said \"\"paid\"\"\"", csv, StringComparison.Ordinal);
        Assert.StartsWith("\uFEFF", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Prefixes_formula_like_cells()
    {
        Assert.Equal("'=1+2", CsvFormatter.MitigateFormulaInjection("=1+2"));
        Assert.Equal("'+SUM(A1)", CsvFormatter.MitigateFormulaInjection("+SUM(A1)"));
        Assert.Equal("'@SUM(A1)", CsvFormatter.MitigateFormulaInjection("@SUM(A1)"));
        Assert.Equal("'-2+3", CsvFormatter.MitigateFormulaInjection("-2+3"));
        Assert.Equal("-10.00", CsvFormatter.MitigateFormulaInjection("-10.00"));
        Assert.Equal("Acme", CsvFormatter.MitigateFormulaInjection("Acme"));
    }

    [Fact]
    public void Formats_dates_and_money_invariantly()
    {
        Assert.Equal("2026-08-22", CsvFormatter.Date(new DateOnly(2026, 8, 22)));
        Assert.Equal("1250.50", CsvFormatter.Money(1250.5m));
        Assert.Equal("-40.00", CsvFormatter.Money(-40m));
        Assert.Equal("Yes", CsvFormatter.Boolean(true));
        Assert.Equal("billfoundry-aging-20260822.csv", CsvFormatter.FileName("aging", new DateOnly(2026, 8, 22)));
    }

    [Fact]
    public void File_name_has_no_path_segments()
    {
        string name = CsvFormatter.FileName(@"..\aging/report", new DateOnly(2026, 1, 2));
        Assert.Equal("billfoundry-aging-report-20260102.csv", name);
        Assert.Equal(name, Path.GetFileName(name));
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain('\\', name);
        Assert.DoesNotContain("..", name, StringComparison.Ordinal);
    }
}

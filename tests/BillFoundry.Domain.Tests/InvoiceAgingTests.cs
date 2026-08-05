using BillFoundry.Domain.Invoices;

namespace BillFoundry.Domain.Tests;

public sealed class InvoiceAgingTests
{
    private static readonly DateOnly AsOf = new(2026, 8, 22);

    [Theory]
    [InlineData(2026, 8, 22, InvoiceAgingBucket.Current)]
    [InlineData(2026, 8, 23, InvoiceAgingBucket.Current)]
    [InlineData(2026, 8, 21, InvoiceAgingBucket.Days1To30)]
    [InlineData(2026, 7, 23, InvoiceAgingBucket.Days1To30)]
    [InlineData(2026, 7, 22, InvoiceAgingBucket.Days31To60)]
    [InlineData(2026, 6, 23, InvoiceAgingBucket.Days31To60)]
    [InlineData(2026, 6, 22, InvoiceAgingBucket.Days61To90)]
    [InlineData(2026, 5, 24, InvoiceAgingBucket.Days61To90)]
    [InlineData(2026, 5, 23, InvoiceAgingBucket.Days90Plus)]
    [InlineData(2025, 1, 1, InvoiceAgingBucket.Days90Plus)]
    public void Bucket_uses_inclusive_day_boundaries(int year, int month, int day, InvoiceAgingBucket expected)
    {
        Assert.Equal(expected, InvoiceAging.Bucket(new DateOnly(year, month, day), AsOf));
    }

    [Fact]
    public void Days_overdue_is_zero_when_due_today_or_later()
    {
        Assert.Equal(0, InvoiceAging.DaysOverdue(AsOf, AsOf));
        Assert.Equal(0, InvoiceAging.DaysOverdue(AsOf.AddDays(3), AsOf));
        Assert.Equal(30, InvoiceAging.DaysOverdue(new DateOnly(2026, 7, 23), AsOf));
        Assert.Equal(91, InvoiceAging.DaysOverdue(new DateOnly(2026, 5, 23), AsOf));
    }
}

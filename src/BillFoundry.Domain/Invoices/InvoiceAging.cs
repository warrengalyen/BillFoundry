namespace BillFoundry.Domain.Invoices;

public enum InvoiceAgingBucket
{
    Current = 0,
    Days1To30 = 1,
    Days31To60 = 2,
    Days61To90 = 3,
    Days90Plus = 4
}

/// <summary>
/// Receivables aging uses the invoice due date relative to an as-of date.
/// Current includes invoices due today. Day 90 overdue is the last day of
/// the 61-90 bucket; 90+ starts at 91 days.
/// </summary>
public static class InvoiceAging
{
    public static InvoiceAgingBucket Bucket(DateOnly dueDate, DateOnly asOf)
    {
        if (dueDate >= asOf)
        {
            return InvoiceAgingBucket.Current;
        }

        int daysOverdue = asOf.DayNumber - dueDate.DayNumber;
        if (daysOverdue <= 30)
        {
            return InvoiceAgingBucket.Days1To30;
        }

        if (daysOverdue <= 60)
        {
            return InvoiceAgingBucket.Days31To60;
        }

        if (daysOverdue <= 90)
        {
            return InvoiceAgingBucket.Days61To90;
        }

        return InvoiceAgingBucket.Days90Plus;
    }

    public static int DaysOverdue(DateOnly dueDate, DateOnly asOf) =>
        dueDate >= asOf ? 0 : asOf.DayNumber - dueDate.DayNumber;

    public static string Label(InvoiceAgingBucket bucket) => bucket switch
    {
        InvoiceAgingBucket.Current => "Current",
        InvoiceAgingBucket.Days1To30 => "1-30 days overdue",
        InvoiceAgingBucket.Days31To60 => "31-60 days overdue",
        InvoiceAgingBucket.Days61To90 => "61-90 days overdue",
        InvoiceAgingBucket.Days90Plus => "90+ days overdue",
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "The aging bucket is not supported.")
    };

    public static IReadOnlyList<InvoiceAgingBucket> All { get; } =
    [
        InvoiceAgingBucket.Current,
        InvoiceAgingBucket.Days1To30,
        InvoiceAgingBucket.Days31To60,
        InvoiceAgingBucket.Days61To90,
        InvoiceAgingBucket.Days90Plus
    ];
}

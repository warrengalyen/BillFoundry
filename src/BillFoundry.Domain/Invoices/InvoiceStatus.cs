namespace BillFoundry.Domain.Invoices;

public enum InvoiceStatus
{
    Draft = 0,
    Sent = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Void = 5
}

public static class InvoiceStatusRules
{
    public static string Label(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Draft",
        InvoiceStatus.Sent => "Sent",
        InvoiceStatus.PartiallyPaid => "Partially paid",
        InvoiceStatus.Paid => "Paid",
        InvoiceStatus.Overdue => "Overdue",
        InvoiceStatus.Void => "Void",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The invoice status is not supported.")
    };

    public static bool IsDefined(InvoiceStatus status) => Enum.IsDefined(status);

    public static bool CanEdit(InvoiceStatus status) => status is InvoiceStatus.Draft;

    public static bool CanVoid(InvoiceStatus status) => status is InvoiceStatus.Draft or InvoiceStatus.Sent;

    public static bool CanTransition(InvoiceStatus from, InvoiceStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return from switch
        {
            InvoiceStatus.Draft => to is InvoiceStatus.Sent or InvoiceStatus.Void,
            InvoiceStatus.Sent => to is InvoiceStatus.Void,
            InvoiceStatus.PartiallyPaid => false,
            InvoiceStatus.Paid => false,
            InvoiceStatus.Overdue => false,
            InvoiceStatus.Void => false,
            _ => false
        };
    }

    public static IReadOnlyList<InvoiceStatus> UserFacingTargets(InvoiceStatus from) => from switch
    {
        InvoiceStatus.Draft => [InvoiceStatus.Sent, InvoiceStatus.Void],
        InvoiceStatus.Sent => [InvoiceStatus.Void],
        _ => []
    };

    public static string ActionLabel(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Sent => "Mark as sent",
        InvoiceStatus.Void => "Void invoice",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The invoice action is not supported.")
    };

    /// <summary>
    /// Overdue is a derived display status. Persisted workflow remains Sent
    /// (or PartiallyPaid) so later payment recording does not lose history.
    /// </summary>
    public static InvoiceStatus EffectiveStatus(
        InvoiceStatus status,
        DateOnly dueDate,
        decimal balanceDue,
        DateOnly today)
    {
        if (IsOverdue(status, dueDate, balanceDue, today))
        {
            return InvoiceStatus.Overdue;
        }

        return status;
    }

    public static bool IsOverdue(
        InvoiceStatus status,
        DateOnly dueDate,
        decimal balanceDue,
        DateOnly today) =>
        status is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid
        && dueDate < today
        && balanceDue > 0m;
}

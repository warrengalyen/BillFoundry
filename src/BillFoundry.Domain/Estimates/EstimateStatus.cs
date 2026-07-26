namespace BillFoundry.Domain.Estimates;

public enum EstimateStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4,
    Converted = 5
}

public static class EstimateStatusRules
{
    public static string Label(EstimateStatus status) => status switch
    {
        EstimateStatus.Draft => "Draft",
        EstimateStatus.Sent => "Sent",
        EstimateStatus.Accepted => "Accepted",
        EstimateStatus.Declined => "Declined",
        EstimateStatus.Expired => "Expired",
        EstimateStatus.Converted => "Converted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The estimate status is not supported.")
    };

    public static bool IsDefined(EstimateStatus status) => Enum.IsDefined(status);

    public static bool CanEdit(EstimateStatus status) => status is EstimateStatus.Draft;

    public static bool CanTransition(EstimateStatus from, EstimateStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return from switch
        {
            EstimateStatus.Draft => to is EstimateStatus.Sent or EstimateStatus.Declined,
            EstimateStatus.Sent => to is EstimateStatus.Draft
                or EstimateStatus.Accepted
                or EstimateStatus.Declined
                or EstimateStatus.Expired,
            EstimateStatus.Accepted => to is EstimateStatus.Converted,
            EstimateStatus.Declined => to is EstimateStatus.Draft,
            EstimateStatus.Expired => to is EstimateStatus.Draft,
            EstimateStatus.Converted => false,
            _ => false
        };
    }

    public static IReadOnlyList<EstimateStatus> AllowedTargets(EstimateStatus from) => from switch
    {
        EstimateStatus.Draft => [EstimateStatus.Sent, EstimateStatus.Declined],
        EstimateStatus.Sent => [EstimateStatus.Draft, EstimateStatus.Accepted, EstimateStatus.Declined, EstimateStatus.Expired],
        EstimateStatus.Accepted => [EstimateStatus.Converted],
        EstimateStatus.Declined => [EstimateStatus.Draft],
        EstimateStatus.Expired => [EstimateStatus.Draft],
        _ => []
    };

    /// <summary>
    /// Statuses an operator may choose in this phase. Conversion to an invoice
    /// is modeled but not exposed until that workflow exists.
    /// </summary>
    public static IReadOnlyList<EstimateStatus> UserFacingTargets(EstimateStatus from) =>
        [.. AllowedTargets(from).Where(status => status is not EstimateStatus.Converted)];

    public static string ActionLabel(EstimateStatus status) => status switch
    {
        EstimateStatus.Draft => "Return to draft",
        EstimateStatus.Sent => "Mark as sent",
        EstimateStatus.Accepted => "Mark as accepted",
        EstimateStatus.Declined => "Mark as declined",
        EstimateStatus.Expired => "Mark as expired",
        EstimateStatus.Converted => "Convert to invoice",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The estimate status is not supported.")
    };
}

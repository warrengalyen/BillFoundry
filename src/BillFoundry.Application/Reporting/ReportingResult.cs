namespace BillFoundry.Application.Reporting;

public sealed class ReportingResult<T>
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public T? Value { get; private init; }

    public static ReportingResult<T> Success(T value) =>
        new()
        {
            Succeeded = true,
            Value = value
        };

    public static ReportingResult<T> Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to view reports."]
        };

    public static ReportingResult<T> Invalid(IReadOnlyList<string> errors) =>
        new()
        {
            Errors = errors
        };
}

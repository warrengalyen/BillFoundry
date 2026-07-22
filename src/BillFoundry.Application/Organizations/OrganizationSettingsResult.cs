namespace BillFoundry.Application.Organizations;

public sealed class OrganizationSettingsResult
{
    public bool Succeeded { get; private init; }

    public bool IsForbidden { get; private init; }

    public bool IsConcurrencyConflict { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public OrganizationSettingsDto? Organization { get; private init; }

    public static OrganizationSettingsResult Success(OrganizationSettingsDto organization) =>
        new()
        {
            Succeeded = true,
            Organization = organization
        };

    public static OrganizationSettingsResult Forbidden() =>
        new()
        {
            IsForbidden = true,
            Errors = ["You are not allowed to manage organization settings."]
        };

    public static OrganizationSettingsResult Invalid(IReadOnlyList<string> errors, OrganizationSettingsDto? organization = null) =>
        new()
        {
            Errors = errors,
            Organization = organization
        };

    public static OrganizationSettingsResult ConcurrencyConflict(OrganizationSettingsDto organization) =>
        new()
        {
            IsConcurrencyConflict = true,
            Organization = organization,
            Errors = ["The organization was updated by another user. Review the current values and save again."]
        };
}

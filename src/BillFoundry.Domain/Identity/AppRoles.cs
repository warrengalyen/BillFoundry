namespace BillFoundry.Domain.Identity;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string User = "User";

    public static IReadOnlyList<string> All { get; } = [Administrator, User];
}

namespace BillFoundry.Web.Security;

internal static class LocalUrl
{
    public static string Resolve(string? returnUrl, string fallback = "/")
    {
        if (IsSafe(returnUrl))
        {
            return returnUrl!;
        }

        return fallback;
    }

    public static bool IsSafe(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
}

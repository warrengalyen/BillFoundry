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

    public static bool IsSafe(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
        {
            return false;
        }

        // Allow "/" or "/foo" but not "//foo", "/\", or URLs with control characters.
        if (returnUrl[0] == '/')
        {
            if (returnUrl.Length == 1)
            {
                return true;
            }

            if (returnUrl[1] != '/' && returnUrl[1] != '\\')
            {
                return !HasControlCharacter(returnUrl.AsSpan(1));
            }

            return false;
        }

        if (returnUrl[0] == '~' && returnUrl.Length > 1 && returnUrl[1] == '/')
        {
            if (returnUrl.Length == 2)
            {
                return true;
            }

            if (returnUrl[2] != '/' && returnUrl[2] != '\\')
            {
                return !HasControlCharacter(returnUrl.AsSpan(2));
            }

            return false;
        }

        return false;
    }

    private static bool HasControlCharacter(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}

using Npgsql;

namespace BillFoundry.Infrastructure.Persistence;

/// <summary>
/// Render injects <c>postgres://</c> URLs. Npgsql's connection-string builder
/// only accepts keyword-value format (<c>Host=…;Username=…</c>).
/// </summary>
internal static class PostgreSqlConnectionString
{
    public static string ToKeywordValue(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        string trimmed = connectionString.Trim();
        if (!IsPostgresUri(trimmed))
        {
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException(
                "The PostgreSQL connection string is not a valid URI or keyword-value string.",
                nameof(connectionString));
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.IdnHost,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'))
        };

        if (!uri.IsDefaultPort && uri.Port > 0)
        {
            builder.Port = uri.Port;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            string[] userInfo = uri.UserInfo.Split(':', 2);
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
            if (userInfo.Length > 1)
            {
                builder.Password = Uri.UnescapeDataString(userInfo[1]);
            }
        }

        ApplyQuery(uri, builder);
        return builder.ConnectionString;
    }

    private static bool IsPostgresUri(string connectionString) =>
        connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static void ApplyQuery(Uri uri, NpgsqlConnectionStringBuilder builder)
    {
        if (string.IsNullOrEmpty(uri.Query) || uri.Query == "?")
        {
            return;
        }

        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=');
            string key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            string value = separator < 0
                ? string.Empty
                : Uri.UnescapeDataString(pair[(separator + 1)..]);

            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse(value, ignoreCase: true, out SslMode sslMode))
            {
                builder.SslMode = sslMode;
            }
        }
    }
}

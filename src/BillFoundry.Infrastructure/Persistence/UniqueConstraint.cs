using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BillFoundry.Infrastructure.Persistence;

internal static class UniqueConstraint
{
    public static bool IsViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.InnerException is SqlException sql)
        {
            return sql.Number is 2601 or 2627;
        }

        return exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}

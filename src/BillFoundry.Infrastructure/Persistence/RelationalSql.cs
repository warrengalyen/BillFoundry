using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillFoundry.Infrastructure.Persistence;

/// <summary>
/// Identifier quoting and a few store literals that differ between SQL Server
/// and PostgreSQL. Used only inside EF configuration.
/// </summary>
internal sealed class RelationalSql(bool isPostgreSql)
{
    public bool IsPostgreSql { get; } = isPostgreSql;

    public string Ident(string name) => IsPostgreSql ? $"\"{name}\"" : $"[{name}]";

    public string TrueLiteral => IsPostgreSql ? "TRUE" : "1";

    public string IsNotNull(string column) => $"{Ident(column)} IS NOT NULL";

    public void ConfigureRowVersion<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, byte[]>> property)
        where TEntity : class
    {
        if (IsPostgreSql)
        {
            builder.Property(property)
                .IsConcurrencyToken()
                .HasColumnType("bytea")
                .ValueGeneratedNever();
            return;
        }

        builder.Property(property).IsRowVersion();
    }
}

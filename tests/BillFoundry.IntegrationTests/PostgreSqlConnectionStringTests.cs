using BillFoundry.Infrastructure.Persistence;
using Npgsql;

namespace BillFoundry.IntegrationTests;

public sealed class PostgreSqlConnectionStringTests
{
    [Fact]
    public void Keyword_connection_strings_are_left_unchanged()
    {
        const string keyword =
            "Host=db;Port=5432;Database=billfoundry;Username=billfoundry;Password=DevOnly_P@ssw0rd;Timeout=30";

        Assert.Equal(keyword, PostgreSqlConnectionString.ToKeywordValue(keyword));
    }

    [Fact]
    public void Render_postgres_url_is_converted_to_npgsql_keyword_format()
    {
        const string url = "postgres://billfoundry:s3cret@dpg-abc123-a/billfoundry";

        var builder = new NpgsqlConnectionStringBuilder(PostgreSqlConnectionString.ToKeywordValue(url));

        Assert.Equal("dpg-abc123-a", builder.Host);
        Assert.Equal("billfoundry", builder.Database);
        Assert.Equal("billfoundry", builder.Username);
        Assert.Equal("s3cret", builder.Password);
    }

    [Fact]
    public void Postgresql_url_preserves_port_encoded_password_and_ssl_mode()
    {
        const string url =
            "postgresql://studio:p%40ss%3Aword@db.example:6543/billfoundry?sslmode=require";

        var builder = new NpgsqlConnectionStringBuilder(PostgreSqlConnectionString.ToKeywordValue(url));

        Assert.Equal("db.example", builder.Host);
        Assert.Equal(6543, builder.Port);
        Assert.Equal("billfoundry", builder.Database);
        Assert.Equal("studio", builder.Username);
        Assert.Equal("p@ss:word", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
    }
}

using Npgsql;
using ProofFit.Api.Data;

namespace ProofFit.Api.Tests;

public class PostgresConnectionTests
{
    [Fact]
    public void Rewrites_a_database_url_as_a_connection_string()
    {
        var parsed = Parse("postgres://app_user:s3cret@db.internal:6543/prooffit");

        Assert.Equal(("db.internal", 6543, "prooffit", "app_user", "s3cret"),
            (parsed.Host, parsed.Port, parsed.Database, parsed.Username, parsed.Password));
    }

    [Fact]
    public void Uses_the_default_port_when_the_url_has_none()
    {
        Assert.Equal(5432, Parse("postgresql://user:pw@db.example.com/app").Port);
    }

    [Fact]
    public void Decodes_characters_escaped_in_the_url()
    {
        var parsed = Parse("postgres://user%40team:p%40ss%2Fword%3A1@host/my%20db");

        Assert.Equal(("user@team", "p@ss/word:1", "my db"), (parsed.Username, parsed.Password, parsed.Database));
    }

    [Theory]
    [InlineData("require", SslMode.Require)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    public void Carries_over_the_ssl_mode(string inUrl, SslMode expected)
    {
        Assert.Equal(expected, Parse($"postgres://user:pw@host/app?sslmode={inUrl}").SslMode);
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=prooffit;Username=prooffit;Password=prooffit")]
    [InlineData("")]
    [InlineData(null)]
    public void Leaves_anything_that_is_not_a_url_unchanged(string? value)
    {
        Assert.Equal(value, PostgresConnection.Normalise(value));
    }

    private static NpgsqlConnectionStringBuilder Parse(string url) => new(PostgresConnection.Normalise(url));
}

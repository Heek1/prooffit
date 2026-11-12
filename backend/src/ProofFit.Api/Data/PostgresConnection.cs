using System.Web;
using Npgsql;

namespace ProofFit.Api.Data;

public static class PostgresConnection
{
    /// <summary>
    /// Hosting platforms hand out a database address as a URL
    /// ("postgres://user:password@host:5432/database"), which Npgsql does not accept. A URL
    /// is rewritten as a connection string; anything else is returned unchanged.
    /// </summary>
    public static string? Normalise(string? value)
    {
        if (value is null ||
            !(value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
              value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
        {
            return value;
        }

        var url = new Uri(value);
        var credentials = url.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = url.Host,
            Port = url.Port > 0 ? url.Port : 5432,
            Database = Uri.UnescapeDataString(url.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
        };
        if (credentials.Length > 1) builder.Password = Uri.UnescapeDataString(credentials[1]);

        // "sslmode=verify-full" in a URL is SslMode.VerifyFull in Npgsql.
        if (HttpUtility.ParseQueryString(url.Query)["sslmode"] is { } sslMode &&
            Enum.TryParse<SslMode>(sslMode.Replace("-", ""), ignoreCase: true, out var mode))
        {
            builder.SslMode = mode;
        }

        return builder.ConnectionString;
    }
}

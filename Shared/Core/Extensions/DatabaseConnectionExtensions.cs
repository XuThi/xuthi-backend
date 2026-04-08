using System.Text;
using Microsoft.Extensions.Configuration;

namespace Core.Extensions;

public static class DatabaseConnectionExtensions
{
    public static string GetPostgresConnectionString(this IConfiguration configuration, string connectionName = "DatabaseConnection")
    {
        var raw = configuration.GetConnectionString(connectionName)
                  ?? configuration[$"ConnectionStrings:{connectionName}"]
                  ?? configuration[connectionName]
                  ?? configuration["DATABASE_URL"]
                  ?? configuration["POSTGRES_URL"]
                  ?? configuration["POSTGRES_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Database connection string is missing. Configure 'ConnectionStrings:{connectionName}' or DATABASE_URL.");
        }

        return NormalizePostgresConnectionString(raw);
    }

    private static string NormalizePostgresConnectionString(string raw)
    {
        var trimmed = raw.Trim();

        if (trimmed.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || !trimmed.Contains("://", StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (!uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase)))
        {
            return trimmed;
        }

        var builder = new StringBuilder();
        AppendPair(builder, "Host", uri.Host);

        if (!uri.IsDefaultPort && uri.Port > 0)
        {
            AppendPair(builder, "Port", uri.Port.ToString());
        }

        var database = uri.AbsolutePath.Trim('/');
        if (!string.IsNullOrWhiteSpace(database))
        {
            AppendPair(builder, "Database", Uri.UnescapeDataString(database));
        }

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var userInfoParts = uri.UserInfo.Split(':', 2);
            if (!string.IsNullOrWhiteSpace(userInfoParts[0]))
            {
                AppendPair(builder, "Username", Uri.UnescapeDataString(userInfoParts[0]));
            }

            if (userInfoParts.Length > 1)
            {
                AppendPair(builder, "Password", Uri.UnescapeDataString(userInfoParts[1]));
            }
        }

        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                var rawKey = Uri.UnescapeDataString(kv[0]);
                var rawValue = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;

                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    continue;
                }

                var key = NormalizeNpgsqlKeyword(rawKey);
                var value = rawValue;

                // Neon samples often use `?sslmode=require`; tolerate malformed `?sslmode`.
                if (key.Equals("Ssl Mode", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(value))
                {
                    value = "Require";
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    AppendPair(builder, key, value);
                }
            }
        }

        return builder.ToString();
    }

    private static string NormalizeNpgsqlKeyword(string rawKey)
    {
        var key = rawKey.Trim().Replace("_", " ").Replace("-", " ");
        return key.ToLowerInvariant() switch
        {
            "sslmode" => "Ssl Mode",
            "ssl mode" => "Ssl Mode",
            "trustservercertificate" => "Trust Server Certificate",
            "trust server certificate" => "Trust Server Certificate",
            "channelbinding" => "Channel Binding",
            "channel binding" => "Channel Binding",
            _ => key,
        };
    }

    private static void AppendPair(StringBuilder builder, string key, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(';');
        }

        builder.Append(key);
        builder.Append('=');
        builder.Append(value.Replace(";", "\\;", StringComparison.Ordinal));
    }
}
using PgCredsHandler.Core.Util;

namespace PgCredsHandler.Scanner.Parsers;

/// <summary>
/// Finds Postgres connections in a set of environment variables. Used for .env files
/// and for the "environment:" block of docker-compose services. Two strategies:
///  1. Any value that is a postgres:// URL (DATABASE_URL, SQLALCHEMY_DATABASE_URI, ...)
///  2. Groups of discrete variables sharing a prefix (PGHOST/PGUSER..., DB_HOST/DB_USER..., POSTGRES_USER...)
/// </summary>
public static class EnvCredentialExtractor
{
    private enum Field { Host, Port, Database, User, Password, Type }

    // Longest suffixes first so "DB_NAME" wins over "NAME".
    private static readonly (string Suffix, Field Field)[] Suffixes =
    {
        ("HOSTNAME", Field.Host), ("HOST", Field.Host), ("SERVER", Field.Host),
        ("PORT", Field.Port),
        ("DATABASE", Field.Database), ("DBNAME", Field.Database), ("NAME", Field.Database), ("DB", Field.Database),
        ("USERNAME", Field.User), ("USER", Field.User),
        ("PASSWORD", Field.Password), ("PASSWD", Field.Password), ("PASS", Field.Password), ("PWD", Field.Password),
        ("CONNECTION", Field.Type), ("ENGINE", Field.Type), ("DIALECT", Field.Type), ("VENDOR", Field.Type),
    };

    public static List<ParsedCredential> Extract(IReadOnlyDictionary<string, string> vars)
    {
        var results = new List<ParsedCredential>();

        // 1) Whole-URL values.
        foreach (var (key, value) in vars)
        {
            if (!PostgresUrl.LooksLikePostgresUrl(value)) continue;
            if (VariableExpander.HasUnresolved(value)) continue;
            if (PostgresUrl.TryParse(value, out var parts) && !parts.IsEmpty)
                results.Add(new ParsedCredential(key, parts));
        }

        // 2) Discrete variables grouped by prefix.
        foreach (var group in GroupByPrefix(vars))
        {
            var parts = BuildFromGroup(group.Value);
            if (parts is null) continue;
            var label = string.IsNullOrEmpty(group.Key) ? "(env vars)" : group.Key.TrimEnd('_');
            results.Add(new ParsedCredential(label, parts));
        }

        return results;
    }

    private static Dictionary<string, Dictionary<Field, string>> GroupByPrefix(IReadOnlyDictionary<string, string> vars)
    {
        var groups = new Dictionary<string, Dictionary<Field, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rawKey, value) in vars)
        {
            if (string.IsNullOrEmpty(value)) continue;
            var key = rawKey.ToUpperInvariant();

            foreach (var (suffix, field) in Suffixes)
            {
                if (!key.EndsWith(suffix, StringComparison.Ordinal)) continue;
                var prefix = key[..^suffix.Length];
                if (prefix.Length > 0 && prefix[^1] != '_') continue; // must break on a word boundary
                if (!IsRelevantPrefix(prefix)) continue;

                if (!groups.TryGetValue(prefix, out var g))
                    groups[prefix] = g = new Dictionary<Field, string>();
                g.TryAdd(field, value);
                break; // first (longest) matching suffix wins
            }
        }

        return groups;
    }

    // Only treat a prefix as a DB config if it names a database/postgres, or declares a postgres type,
    // so unrelated vars (MAIL_HOST, REDIS_PORT, ...) are ignored.
    private static bool IsRelevantPrefix(string prefix)
    {
        if (prefix.Length == 0) return false; // bare HOST/PORT with no prefix is too risky
        var p = prefix.TrimEnd('_');
        return p is "PG" or "POSTGRES" or "POSTGRESQL"
            || p.EndsWith("DB") || p.EndsWith("DATABASE") || p.EndsWith("POSTGRES") || p.EndsWith("PG")
            || p is "DATABASE" or "DB";
    }

    private static PgParts? BuildFromGroup(Dictionary<Field, string> g)
    {
        if (g.TryGetValue(Field.Type, out var type) && !IsPostgresType(type)) return null;

        var hasAny = g.ContainsKey(Field.Host) || g.ContainsKey(Field.User)
                     || g.ContainsKey(Field.Password) || g.ContainsKey(Field.Database);
        if (!hasAny) return null;

        // A group with only a port tells us nothing useful.
        if (g.Count == 1 && g.ContainsKey(Field.Port)) return null;

        int? port = g.TryGetValue(Field.Port, out var ps) && int.TryParse(ps, out var pp) ? pp : null;
        var host = g.GetValueOrDefault(Field.Host);
        if (host is not null && port is null)
        {
            var (h, hp) = PostgresUrl.SplitHostPort(host);
            host = h; port = hp;
        }

        return new PgParts(host, port, g.GetValueOrDefault(Field.Database),
                           g.GetValueOrDefault(Field.User), g.GetValueOrDefault(Field.Password));
    }

    private static bool IsPostgresType(string value)
    {
        var v = value.ToLowerInvariant();
        return v.Contains("postgres") || v.Contains("pgsql") || v.Contains("psql") || v == "pg";
    }
}

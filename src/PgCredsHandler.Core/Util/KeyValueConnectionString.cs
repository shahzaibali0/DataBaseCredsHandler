namespace PgCredsHandler.Core.Util;

/// <summary>
/// Parses ADO.NET style connection strings, e.g.
/// "Host=localhost;Port=5432;Database=shop;Username=postgres;Password=secret".
/// </summary>
public static class KeyValueConnectionString
{
    public static Dictionary<string, string> Parse(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < value.Length)
        {
            // key
            var eq = value.IndexOf('=', i);
            if (eq < 0) break;
            var key = value[i..eq].Trim().TrimStart(';').Trim();
            i = eq + 1;

            // value (may be quoted)
            string v;
            while (i < value.Length && value[i] == ' ') i++;
            if (i < value.Length && (value[i] == '"' || value[i] == '\''))
            {
                var q = value[i];
                var sb = new System.Text.StringBuilder();
                i++;
                while (i < value.Length)
                {
                    if (value[i] == q)
                    {
                        if (i + 1 < value.Length && value[i + 1] == q) { sb.Append(q); i += 2; continue; }
                        i++;
                        break;
                    }
                    sb.Append(value[i]);
                    i++;
                }
                v = sb.ToString();
                var semi = value.IndexOf(';', i);
                i = semi < 0 ? value.Length : semi + 1;
            }
            else
            {
                var semi = value.IndexOf(';', i);
                v = (semi < 0 ? value[i..] : value[i..semi]).Trim();
                i = semi < 0 ? value.Length : semi + 1;
            }

            if (key.Length > 0) result[key] = v;
        }
        return result;
    }

    private static readonly string[] HostKeys = { "host", "server", "hostname" };
    private static readonly string[] PortKeys = { "port" };
    private static readonly string[] DbKeys = { "database", "db", "dbname", "initial catalog" };
    private static readonly string[] UserKeys = { "username", "user id", "userid", "user", "uid", "user name" };
    private static readonly string[] PassKeys = { "password", "pwd", "pass" };

    // Keys that only make sense for SQL Server / SQLite / MySQL connection strings.
    private static readonly string[] NonPostgresKeys =
    {
        "initial catalog", "trusted_connection", "integrated security", "multipleactiveresultsets",
        "attachdbfilename", "data source", "allowuservariables", "encrypt", "mode"
    };

    /// <summary>
    /// Tries to read a Postgres connection from a key=value string.
    /// <paramref name="projectUsesNpgsql"/> lowers the bar when the project references Npgsql,
    /// because "Server=...;User Id=..." is otherwise ambiguous with SQL Server.
    /// </summary>
    public static bool TryParsePostgres(string value, bool projectUsesNpgsql, out PgParts parts)
    {
        parts = new PgParts(null, null, null, null, null);
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('=')) return false;

        var kv = Parse(value);
        if (kv.Count < 2) return false;
        if (kv.Keys.Any(k => NonPostgresKeys.Contains(k.ToLowerInvariant()))) return false;

        var host = First(kv, HostKeys);
        var portText = First(kv, PortKeys);
        int? port = int.TryParse(portText, out var p) ? p : null;
        var db = First(kv, DbKeys);
        var user = First(kv, UserKeys);
        var pass = First(kv, PassKeys);

        if (host is null && db is null) return false;

        var hasHostKey = kv.ContainsKey("host");
        var hasUsernameKey = kv.ContainsKey("username");
        var pgPort = port is >= 5432 and <= 5439;

        var looksPostgres = hasHostKey || hasUsernameKey || pgPort || projectUsesNpgsql;
        if (!looksPostgres) return false;

        // Host may contain a port ("localhost:5433") in some configs.
        if (host is not null && port is null)
        {
            var (h, hp) = PostgresUrl.SplitHostPort(host);
            host = h;
            port = hp;
        }

        parts = new PgParts(host, port, db, user, pass);
        return true;
    }

    private static string? First(Dictionary<string, string> kv, string[] keys)
    {
        foreach (var k in keys)
            if (kv.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                return v;
        return null;
    }
}

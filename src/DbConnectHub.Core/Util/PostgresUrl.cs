using System.Text.RegularExpressions;

namespace DbConnectHub.Core.Util;

/// <summary>Connection parts parsed from some config value. Missing parts are null.</summary>
public sealed record PgParts(string? Host, int? Port, string? Database, string? Username, string? Password)
{
    public bool IsEmpty => Host is null && Port is null && Database is null && Username is null && Password is null;
}

/// <summary>
/// Parses and builds postgres:// URLs. Parsing is done by hand (not System.Uri)
/// because real-world .env files often contain unencoded special characters in passwords.
/// </summary>
public static partial class PostgresUrl
{
    [GeneratedRegex(@"^(?:jdbc:)?(?:postgres|postgresql|pgsql)(?:\+[a-z0-9_]+)?://", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeRegex();

    public static bool LooksLikePostgresUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SchemeRegex().IsMatch(value.Trim().Trim('"', '\''));

    public static bool TryParse(string? value, out PgParts parts)
    {
        parts = new PgParts(null, null, null, null, null);
        if (string.IsNullOrWhiteSpace(value)) return false;

        var s = value.Trim().Trim('"', '\'');
        var m = SchemeRegex().Match(s);
        if (!m.Success) return false;

        var rest = s[m.Length..];

        string? user = null, password = null, host = null, db = null;
        int? port = null;

        // Split off the query string (only after the last '@' so '?' in a password doesn't break it).
        var at = rest.LastIndexOf('@');
        var queryStart = rest.IndexOf('?', at < 0 ? 0 : at);
        var query = queryStart >= 0 ? rest[(queryStart + 1)..] : "";
        if (queryStart >= 0) rest = rest[..queryStart];

        if (at >= 0)
        {
            var userInfo = rest[..at];
            rest = rest[(at + 1)..];
            var colon = userInfo.IndexOf(':');
            if (colon >= 0)
            {
                user = Decode(userInfo[..colon]);
                password = Decode(userInfo[(colon + 1)..]);
            }
            else
            {
                user = Decode(userInfo);
            }
        }

        var slash = rest.IndexOf('/');
        var hostPort = slash >= 0 ? rest[..slash] : rest;
        if (slash >= 0)
        {
            var path = rest[(slash + 1)..];
            if (path.Length > 0) db = Decode(path);
        }

        // Multiple hosts "h1:5432,h2:5432" -> take the first.
        var comma = hostPort.IndexOf(',');
        if (comma >= 0) hostPort = hostPort[..comma];

        (host, port) = SplitHostPort(hostPort);

        // libpq allows ?host=&port=&user=&password=&dbname=
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var k = pair[..eq].ToLowerInvariant();
            var v = Decode(pair[(eq + 1)..]);
            switch (k)
            {
                case "host" when host is null && !v.StartsWith('/'): host = v; break;
                case "port" when port is null && int.TryParse(v, out var p): port = p; break;
                case "user" when user is null: user = v; break;
                case "password" when password is null: password = v; break;
                case "dbname" when db is null: db = v; break;
            }
        }

        parts = new PgParts(NullIfEmpty(host), port, NullIfEmpty(db), NullIfEmpty(user), password);
        return true;
    }

    public static (string? host, int? port) SplitHostPort(string hostPort)
    {
        if (string.IsNullOrWhiteSpace(hostPort)) return (null, null);
        hostPort = hostPort.Trim();

        // [::1]:5432
        if (hostPort.StartsWith('['))
        {
            var close = hostPort.IndexOf(']');
            if (close > 0)
            {
                var h = hostPort[1..close];
                var after = hostPort[(close + 1)..];
                int? p = after.StartsWith(':') && int.TryParse(after[1..], out var pp) ? pp : null;
                return (h, p);
            }
        }

        var colon = hostPort.LastIndexOf(':');
        if (colon > 0 && hostPort.IndexOf(':') == colon && int.TryParse(hostPort[(colon + 1)..], out var port))
            return (Decode(hostPort[..colon]), port);

        return (Decode(hostPort), null);
    }

    public static string ToUrl(string host, int port, string database, string username, string? password)
    {
        var userInfo = Uri.EscapeDataString(username);
        if (!string.IsNullOrEmpty(password)) userInfo += ":" + Uri.EscapeDataString(password);
        var h = host.Contains(':') ? $"[{host}]" : host;
        return $"postgresql://{userInfo}@{h}:{port}/{Uri.EscapeDataString(database)}";
    }

    public static string ToJdbc(string host, int port, string database)
    {
        var h = host.Contains(':') ? $"[{host}]" : host;
        return $"jdbc:postgresql://{h}:{port}/{database}";
    }

    private static string Decode(string s)
    {
        try { return Uri.UnescapeDataString(s); }
        catch { return s; }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

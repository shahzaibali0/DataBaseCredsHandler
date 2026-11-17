using System.Net;

namespace DbConnectHub.Core.Util;

/// <summary>
/// Recognises template values from example files, such as
/// <c>postgresql://USER:PASSWORD@HOST:PORT/DATABASE</c> or <c>postgresql://user:password@host:port/dbname</c>.
/// They describe the shape of a connection, not a real database, so the scanner drops them.
/// </summary>
public static class PlaceholderValues
{
    private static readonly HashSet<string> HostWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "host", "hostname", "your-host", "your_host", "yourhost", "db-host", "db_host", "your-db-host", "your_db_host",
    };

    private static readonly HashSet<string> DatabaseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "database", "dbname", "db_name", "database_name", "your_database", "your-database", "your_db", "your-db",
    };

    private static readonly HashSet<string> UserWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "user", "username", "your_user", "your-user", "your_username", "db_user", "dbuser",
    };

    /// <summary>
    /// True when the host is a template word or still contains a word-port (<c>HOST:PORT</c>), when any part is a
    /// bracketed token (<c>&lt;host&gt;</c>, <c>{user}</c>, <c>[db]</c>), or when both the database and the user are
    /// template words. A database called "database" with a real user, or a user called "user" with a real
    /// database, is kept.
    /// </summary>
    public static bool LooksLikePlaceholder(string host, string database, string username) =>
        IsTemplateHost(host)
        || IsBracketedToken(database) || IsBracketedToken(username)
        || (DatabaseWords.Contains(database) && UserWords.Contains(username));

    /// <summary>
    /// True for a password that only says what to put there: YOUR_POSTGRES_PASSWORD, &lt;password&gt;, ${DB_PASSWORD},
    /// changeme, ********. A short real password such as "postgres" or "password" is kept: many local setups use one.
    /// </summary>
    public static bool IsTemplatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        var value = password.Trim();
        return value.StartsWith("your", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("${", StringComparison.Ordinal)
               || IsBracketedToken(value)
               || value.All(character => character is '*' or 'x' or 'X')
               || value.Replace("-", "").Replace("_", "").Equals("changeme", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemplateHost(string host) =>
        HostWords.Contains(host) || IsBracketedToken(host) || HasWordPort(host);

    // "HOST:PORT" reaches us as the host when the port wasn't a number; a real IPv6 address also has colons.
    private static bool HasWordPort(string host) => host.Contains(':') && !IPAddress.TryParse(host, out _);

    private static bool IsBracketedToken(string value) =>
        value.Length > 2 && (value[0], value[^1]) is ('<', '>') or ('{', '}') or ('[', ']');
}

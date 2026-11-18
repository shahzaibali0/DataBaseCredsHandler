using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;

namespace DbConnectHub.Presentation;

/// <summary>
/// Finds the stored password for a database login, for pgAdmin's "password command": pgAdmin runs the app when the
/// user connects, and the password goes from Credential Manager straight to pgAdmin, never into a file.
/// </summary>
public static class SavedPasswordLookup
{
    /// <summary>
    /// The password saved for this host (any case), port, user and database, from current entries if any, else stale ones.
    /// Null when there is none, or when entries disagree: guessing could send one project's password to another's
    /// server, so pgAdmin asks instead.
    /// </summary>
    public static string? FindPassword(DataStores stores, DatabaseLogin login)
    {
        var matches = stores.Connections.GetAll().Where(connection => connection.HasPassword && IsSameLogin(connection, login)).ToList();
        var current = matches.Where(connection => !connection.IsStale).ToList();
        var passwords = (current.Count > 0 ? current : matches)
            .Select(connection => stores.Credentials.Read(connection.Id))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return passwords.Count == 1 ? passwords[0] : null;
    }

    private static bool IsSameLogin(SavedConnection connection, DatabaseLogin login)
    {
        var isSameEndpoint = connection.Port == login.Port
            && string.Equals(connection.Host, login.Host, StringComparison.OrdinalIgnoreCase);
        return isSameEndpoint && connection.Username == login.Username && connection.Database == login.Database;
    }
}

namespace DbConnectHub.Core.Util;

public static class ConnectionKey
{
    /// <summary>
    /// Identity of a connection inside a project. Two entries with the same
    /// project + host + port + database + user are the same connection.
    /// </summary>
    public static string Create(string projectPath, string host, int port, string database, string username)
    {
        var path = NormalizePath(projectPath);
        return string.Join('|', path, host.Trim().ToLowerInvariant(), port, database.Trim(), username.Trim());
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var p = path.Replace('\\', '/').TrimEnd('/');
        return p.ToLowerInvariant();
    }

    /// <summary>True if <paramref name="path"/> is <paramref name="root"/> or inside it.</summary>
    public static bool IsUnder(string path, string root)
    {
        var p = NormalizePath(path);
        var r = NormalizePath(root);
        if (r.Length == 0) return false;
        return p == r || p.StartsWith(r + "/", StringComparison.Ordinal);
    }
}

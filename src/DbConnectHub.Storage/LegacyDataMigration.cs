using System.Text.Json;
using DbConnectHub.Core.Abstractions;

namespace DbConnectHub.Storage;

/// <summary>What the move from the app's old name did.</summary>
public sealed record MigrationResult(int FilesCopied, int PasswordsMoved);

/// <summary>
/// The app used to be called "PgCreds Handler". On the first start under the new name, this moves the user's data
/// across so nothing is lost: the files from %APPDATA%\PgCredsHandler are copied into the new data folder, and each
/// password is copied from "PgCredsHandler:&lt;id&gt;" to the new name in Credential Manager before the old entry is
/// deleted. It only runs when the new folder has no saved list yet, so it never overwrites anything.
/// </summary>
public static class LegacyDataMigration
{
    public const string LegacyName = "PgCredsHandler";

    public static string DefaultLegacyFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyName);

    public static MigrationResult Run(string legacyFolder, string newFolder, ICredentialStore legacyCredentials, ICredentialStore newCredentials)
    {
        var newConnections = Path.Combine(newFolder, "connections.json");
        if (!Directory.Exists(legacyFolder) || File.Exists(newConnections)) return new MigrationResult(0, 0);

        Directory.CreateDirectory(newFolder);
        var filesCopied = 0;
        foreach (var file in Directory.EnumerateFiles(legacyFolder))
        {
            var target = Path.Combine(newFolder, Path.GetFileName(file));
            if (File.Exists(target)) continue;
            File.Copy(file, target);
            filesCopied++;
        }

        return new MigrationResult(filesCopied, MovePasswords(newConnections, legacyCredentials, newCredentials));
    }

    // Copy first, delete after: if a copy fails, the old entry is still there for the next start to try again.
    private static int MovePasswords(string connectionsFile, ICredentialStore legacyCredentials, ICredentialStore newCredentials)
    {
        if (!File.Exists(connectionsFile)) return 0;

        var moved = 0;
        foreach (var (connectionId, username) in ReadConnectionIds(connectionsFile))
        {
            var password = legacyCredentials.Read(connectionId);
            if (password is null) continue;
            if (newCredentials.Read(connectionId) is null) newCredentials.Save(connectionId, username, password);
            legacyCredentials.Delete(connectionId);
            moved++;
        }
        return moved;
    }

    private static IEnumerable<(string Id, string Username)> ReadConnectionIds(string connectionsFile)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(connectionsFile));
        return document.RootElement.EnumerateArray()
            .Select(connection => (
                connection.GetProperty("id").GetString() ?? "",
                connection.TryGetProperty("username", out var username) ? username.GetString() ?? "" : ""))
            .Where(connection => connection.Item1.Length > 0)
            .ToList();
    }
}

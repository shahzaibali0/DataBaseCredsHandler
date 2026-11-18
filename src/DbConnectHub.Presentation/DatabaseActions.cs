using DbConnectHub.Core.Abstractions;

namespace DbConnectHub.Presentation;

/// <summary>The database clients a connection can be opened in.</summary>
public enum DatabaseTool
{
    DBeaver,
    PgAdmin,
    DbVisualizer,
}

/// <summary>What a client needs to connect. The password is read just before launching and not kept.</summary>
public sealed record DatabaseLaunchRequest(string Name, string Host, int Port, string Database, string Username, string? Password)
{
    public override string ToString() => $"{Username}@{Host}:{Port}/{Database}"; // never the password
}

public sealed record ToolLaunch(bool Success, string Message);

/// <summary>Finds and opens database clients (implemented with DbConnectHub.Integrations in the app).</summary>
public interface IDatabaseTools
{
    bool IsInstalled(DatabaseTool tool);

    Task<ToolLaunch> OpenAsync(DatabaseTool tool, DatabaseLaunchRequest request);
}

/// <summary>The clipboard. A secret is kept out of clipboard history and cleared again after a short while.</summary>
public interface IClipboard
{
    void CopyText(string text, bool isSecret);
}

/// <summary>
/// What a database row can do: read its password from the credential store when the user asks, copy values,
/// and open the connection in DBeaver, pgAdmin or DbVisualizer. Reports what happened through <see cref="Notice"/>.
/// </summary>
public sealed class DatabaseActions
{
    private readonly ICredentialStore _credentials;
    private readonly IDatabaseTools _tools;
    private readonly IClipboard _clipboard;
    private Dictionary<DatabaseTool, bool> _installed = [];

    public DatabaseActions(ICredentialStore credentials, IDatabaseTools tools, IClipboard clipboard)
    {
        _credentials = credentials;
        _tools = tools;
        _clipboard = clipboard;
        RefreshInstalledTools();
    }

    /// <summary>A message for the user, and whether it is a problem.</summary>
    public event Action<string, bool>? Notice;

    public bool IsInstalled(DatabaseTool tool) => _installed.GetValueOrDefault(tool);

    /// <summary>Checks again which clients are installed (after the user installs one).</summary>
    public void RefreshInstalledTools() =>
        _installed = Enum.GetValues<DatabaseTool>().ToDictionary(tool => tool, _tools.IsInstalled);

    public string? ReadPassword(string connectionId)
    {
        var password = _credentials.Read(connectionId);
        if (password is null) Notice?.Invoke("That password isn't in Windows Credential Manager any more. Rescan to store it again.", true);
        return password;
    }

    public void CopyPassword(string connectionId)
    {
        var password = ReadPassword(connectionId);
        if (password is null) return;
        _clipboard.CopyText(password, isSecret: true);
        Notice?.Invoke("Password copied. It's kept out of clipboard history and cleared in 30 seconds.", false);
    }

    public void CopyText(string label, string text)
    {
        _clipboard.CopyText(text, isSecret: false);
        Notice?.Invoke($"{label} copied.", false);
    }

    /// <summary>What the screen says while a client is being opened; pgAdmin first updates its server list.</summary>
    public static string OpeningText(DatabaseTool tool) => tool switch
    {
        DatabaseTool.DBeaver => "Starting DBeaver and connecting…",
        DatabaseTool.PgAdmin => "Adding the server to pgAdmin, then starting it. This can take 20 seconds…",
        _ => "Starting DbVisualizer…",
    };

    public async Task OpenAsync(DatabaseTool tool, ConnectionRowViewModel database)
    {
        // Only DBeaver takes the password here. pgAdmin fetches it later through its password command, and
        // DbVisualizer gets a URL, so it isn't read for them.
        var isPasswordNeeded = tool == DatabaseTool.DBeaver && database.HasPassword;
        var password = isPasswordNeeded ? _credentials.Read(database.Id) : null;
        var request = new DatabaseLaunchRequest(
            $"{database.WorkspaceName} · {database.Database}", database.Host, database.Port, database.Database, database.Username, password);

        var launch = await _tools.OpenAsync(tool, request);
        if (launch.Success && tool == DatabaseTool.DbVisualizer) _clipboard.CopyText(database.JdbcUrl, isSecret: false);
        Notice?.Invoke(launch.Message, !launch.Success);
    }
}

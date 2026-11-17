namespace DbConnectHub.Storage;

public static class Paths
{
    /// <summary>
    /// Environment variable that points the app at another data folder, e.g. to try it with demo projects
    /// without touching your real list. Passwords still go to Windows Credential Manager.
    /// </summary>
    public const string DataFolderVariable = "DBCONNECTHUB_DATA_DIR";

    /// <summary>%APPDATA%\DbConnectHub, or the folder in <see cref="DataFolderVariable"/> when it is set.</summary>
    public static string DataFolder
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable(DataFolderVariable) is { Length: > 0 } custom
                ? custom
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DbConnectHub");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string ConnectionsFile => Path.Combine(DataFolder, "connections.json");
    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
    public static string InventoryFile => Path.Combine(DataFolder, "inventory.json");

    /// <summary>Prefix for Windows Credential Manager target names.</summary>
    public const string CredentialPrefix = "DbConnectHub";
}

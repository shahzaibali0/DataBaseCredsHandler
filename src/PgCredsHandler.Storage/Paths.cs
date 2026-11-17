namespace PgCredsHandler.Storage;

public static class Paths
{
    /// <summary>%APPDATA%\PgCredsHandler</summary>
    public static string DataFolder
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "PgCredsHandler");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string ConnectionsFile => Path.Combine(DataFolder, "connections.json");
    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
    public static string InventoryFile => Path.Combine(DataFolder, "inventory.json");

    /// <summary>Prefix for Windows Credential Manager target names.</summary>
    public const string CredentialPrefix = "PgCredsHandler";
}

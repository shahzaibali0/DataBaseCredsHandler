using System.Text.Json;

namespace DbConnectHub.Integrations;

/// <summary>
/// Opens a database in a client with the connection already set up:
/// DBeaver connects straight away (password included); pgAdmin gets the server added (it asks for the password
/// once, because writing it to a file would break the rule that passwords live only in Credential Manager);
/// DbVisualizer is started and the caller copies the connection URL for its "Create Connection" box.
/// </summary>
public sealed class DbToolLauncher
{
    // pgAdmin's desktop mode keeps everything under this built-in user.
    private const string PgAdminDesktopUser = "pgadmin4@pgadmin.org";
    private static readonly TimeSpan PgAdminImportTimeout = TimeSpan.FromSeconds(60);

    // Where the pgAdmin desktop app keeps its servers, so the import lands in the database the window reads.
    private static readonly string PgAdminDesktopDatabase =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "pgAdmin", "pgadmin4.db");

    private readonly DbToolLocator _locator;
    private readonly IProcessRunner _processes;

    public DbToolLauncher(DbToolLocator locator, IProcessRunner processes)
    {
        _locator = locator;
        _processes = processes;
    }

    public bool IsInstalled(DbTool tool) => _locator.Find(tool) is not null;

    public LaunchResult Open(DbTool tool, DbConnectionInfo connection)
    {
        var executable = _locator.Find(tool);
        if (executable is null) return new LaunchResult(false, $"{DisplayName(tool)} isn't installed on this PC.");

        return tool switch
        {
            DbTool.DBeaver => OpenInDbeaver(executable, connection),
            DbTool.PgAdmin => OpenInPgAdmin(executable, connection),
            _ => StartOnly(executable, "DbVisualizer opened. The connection URL is on your clipboard: choose Create Connection and paste it."),
        };
    }

    public static string DisplayName(DbTool tool) => tool switch
    {
        DbTool.DBeaver => "DBeaver",
        DbTool.PgAdmin => "pgAdmin",
        _ => "DbVisualizer",
    };

    /// <summary>DBeaver's -con option: "key=value|key=value…", so a value can't contain "|".</summary>
    public static IReadOnlyList<string>? DbeaverArguments(DbConnectionInfo connection)
    {
        var values = new (string Key, string? Value)[]
        {
            ("driver", "postgresql"), ("host", connection.Host), ("port", connection.Port.ToString()),
            ("database", connection.Database), ("user", connection.Username), ("password", connection.Password),
            ("name", connection.Name), ("connect", "true"),
        };
        if (values.Any(pair => pair.Value?.Contains('|') == true)) return null;

        return ["-con", string.Join('|', values.Where(pair => pair.Value is not null).Select(pair => $"{pair.Key}={pair.Value}"))];
    }

    /// <summary>A pgAdmin servers.json with one server and no password field.</summary>
    public static string PgAdminServersJson(DbConnectionInfo connection) => JsonSerializer.Serialize(new
    {
        Servers = new Dictionary<string, object>
        {
            ["1"] = new
            {
                Name = connection.Name,
                Group = "DB Connect Hub",
                Host = connection.Host,
                Port = connection.Port,
                MaintenanceDB = connection.Database,
                Username = connection.Username,
                SSLMode = "prefer",
            },
        },
    });

    private LaunchResult OpenInDbeaver(string executable, DbConnectionInfo connection)
    {
        var arguments = DbeaverArguments(connection);
        if (arguments is null)
            return new LaunchResult(false, "DBeaver can't take a value that contains \"|\". Copy the details into DBeaver instead.");

        _processes.Start(executable, arguments);
        return new LaunchResult(true, $"DBeaver is opening {connection.Database}, already connected.");
    }

    private LaunchResult OpenInPgAdmin(string executable, DbConnectionInfo connection)
    {
        var added = TryAddServerToPgAdmin(executable, connection);
        _processes.Start(executable, []);
        return added
            ? new LaunchResult(true, $"pgAdmin is opening with {connection.Name} under \"DB Connect Hub\". It asks for the password once: use Copy here.")
            : new LaunchResult(true, "pgAdmin is opening, but the server couldn't be added for you. Use the details here to add it.");
    }

    // pgAdmin's own setup script imports a servers.json into its desktop database.
    private bool TryAddServerToPgAdmin(string executable, DbConnectionInfo connection)
    {
        var installFolder = Directory.GetParent(Path.GetDirectoryName(executable)!)?.FullName;
        if (installFolder is null) return false;
        var python = Path.Combine(installFolder, "python", "python.exe");
        var setupScript = Path.Combine(installFolder, "web", "setup.py");
        if (!File.Exists(python) || !File.Exists(setupScript)) return false;

        var serversFile = Path.Combine(Path.GetTempPath(), $"dbconnecthub-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(serversFile, PgAdminServersJson(connection));
            var arguments = new List<string> { setupScript, "load-servers", serversFile, "--user", PgAdminDesktopUser };
            if (File.Exists(PgAdminDesktopDatabase)) arguments.AddRange(["--sqlite-path", PgAdminDesktopDatabase]);
            var exitCode = _processes.Run(python, arguments, PgAdminImportTimeout);
            if (exitCode != 0) exitCode = _processes.Run(python, [setupScript, "--load-servers", serversFile], PgAdminImportTimeout); // pgAdmin 6
            return exitCode == 0;
        }
        finally
        {
            File.Delete(serversFile);
        }
    }

    private LaunchResult StartOnly(string executable, string message)
    {
        _processes.Start(executable, []);
        return new LaunchResult(true, message);
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using DbConnectHub.Core.Models;

namespace DbConnectHub.Integrations;

/// <summary>What happened when a connection was handed to pgAdmin's server list.</summary>
public enum PgAdminServerOutcome
{
    Added,
    AlreadyThere,

    /// <summary>The database is there, but without the current password command (an older entry); the user deletes it.</summary>
    OutdatedEntry,
    CouldNotAdd,
}

/// <summary>
/// Keeps one pgAdmin server per opened database in a "DB Connect Hub" group, named after its project and restricted to
/// that database, using pgAdmin's own setup.py: dump-servers to see what is there, load-servers to add what is missing.
/// The import file never holds a password: each server gets a PasswordExecCommand that runs this app, which reads the
/// password from Credential Manager.
/// </summary>
public sealed class PgAdminServers
{
    public const string GroupName = "DB Connect Hub";

    /// <summary>The argument that makes DbConnectHub.exe print a login's password for pgAdmin and exit.</summary>
    public const string PasswordArgument = "--pgadmin-password";

    // pgAdmin's desktop mode keeps everything under this built-in user.
    private const string DesktopUser = "pgadmin4@pgadmin.org";
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromSeconds(90);

    // setup.py prints its errors and still exits with 0, so success is read from this line instead.
    private static readonly Regex AddedServersLine = new(@"Added \d+ Server Group\(s\) and (?<servers>\d+) Server\(s\)");

    /// <summary>Where the pgAdmin desktop app keeps its servers, so the import lands in the database the window reads.</summary>
    public static readonly string DesktopDatabase =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "pgAdmin", "pgadmin4.db");

    private readonly IProcessRunner _processes;
    private readonly string? _appPath;
    private readonly string _desktopDatabase;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    private ServerListing? _lastListing;

    /// <param name="processes">Runs pgAdmin's setup.py.</param>
    /// <param name="appPath">This app's exe, for pgAdmin's password command; null leaves the password to pgAdmin.</param>
    /// <param name="desktopDatabase">pgAdmin's pgadmin4.db, normally <see cref="DesktopDatabase"/>.</param>
    public PgAdminServers(IProcessRunner processes, string? appPath, string desktopDatabase)
    {
        _processes = processes;
        _appPath = appPath;
        _desktopDatabase = desktopDatabase;
    }

    /// <summary>
    /// The command pgAdmin runs, through cmd.exe, to get the password: the quoted exe path and a hex key, nothing
    /// else. Null when cmd.exe could misread the path, so pgAdmin asks instead: a quote ends the quoting, % and
    /// (with delayed expansion on) ! expand even inside quotes, and ^ is refused too as it costs nothing.
    /// </summary>
    public static string? PasswordCommand(DbConnectionInfo connection, string? appPath)
    {
        if (appPath is null || appPath.IndexOfAny(['"', '%', '!', '^']) >= 0) return null;
        var login = new DatabaseLogin(connection.Host, connection.Port, connection.Username, connection.Database);
        return $"\"{appPath}\" {PasswordArgument} {login.ToKey()}";
    }

    /// <summary>
    /// Whether a password survives pgAdmin's command read unchanged: pgAdmin strips the output and its Python may
    /// decode it as cp1252 or UTF-8, so only printable ASCII without outer spaces is sent; anything else pgAdmin asks for.
    /// </summary>
    public static bool CanSendPassword(string password) =>
        password.Length > 0 && password == password.Trim() && password.All(character => character is >= ' ' and <= '~');

    /// <summary>
    /// A pgAdmin servers.json with one server and no password field. The server is named after the project and
    /// restricted to this one database, so pgAdmin shows only it, not every database on the same Postgres.
    /// </summary>
    public static string ServersJson(DbConnectionInfo connection, string? appPath)
    {
        var server = new Dictionary<string, object>
        {
            ["Name"] = connection.Name,
            ["Group"] = GroupName,
            ["Host"] = connection.Host,
            ["Port"] = connection.Port,
            ["MaintenanceDB"] = connection.Database,
            ["Username"] = connection.Username,
            ["DBRestriction"] = connection.Database,
            ["DBRestrictionType"] = "databases",
            ["SSLMode"] = "prefer",
        };
        var passwordCommand = PasswordCommand(connection, appPath);
        if (passwordCommand is not null) server["PasswordExecCommand"] = passwordCommand;
        return JsonSerializer.Serialize(new { Servers = new Dictionary<string, object> { ["1"] = server } });
    }

    // One at a time: two opens of the same database running side by side would both see it missing and add it twice.
    public async Task<PgAdminServerOutcome> AddIfMissingAsync(
        string pgAdminExecutable, DbConnectionInfo connection, CancellationToken cancellationToken)
    {
        await _oneAtATime.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await AddIfMissingAloneAsync(pgAdminExecutable, connection, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    private async Task<PgAdminServerOutcome> AddIfMissingAloneAsync(
        string pgAdminExecutable, DbConnectionInfo connection, CancellationToken cancellationToken)
    {
        var setup = FindSetup(pgAdminExecutable);
        if (setup is null) return PgAdminServerOutcome.CouldNotAdd;

        var existing = await ListServersAsync(setup, cancellationToken).ConfigureAwait(false);
        if (existing is null) return PgAdminServerOutcome.CouldNotAdd;
        var sameEntry = existing.FirstOrDefault(server => IsSameEntry(server, connection));
        if (sameEntry is not null) return CompareEntry(sameEntry, connection);

        // Forgotten before the import, so an import that fails or is cancelled halfway is never covered by the old list.
        _lastListing = null;
        var isAdded = await ImportAsync(setup, connection, cancellationToken).ConfigureAwait(false);
        return isAdded ? PgAdminServerOutcome.Added : PgAdminServerOutcome.CouldNotAdd;
    }

    private sealed record SetupScript(string Python, string Script, string RuntimeFolder);

    private sealed record ListedServer(
        string Name, string Group, string Host, int Port, string Username, string Database, string PasswordCommand);

    /// <summary>pgadmin4.db's size and write times; any change to pgAdmin's servers changes at least one.</summary>
    private sealed record DatabaseStamp(long Length, DateTime WrittenAtUtc, DateTime JournalWrittenAtUtc);

    private sealed record ServerListing(DatabaseStamp Stamp, IReadOnlyList<ListedServer> Servers);

    // An entry with another name or password command comes from an older version (or the app moved). setup.py can't
    // edit it, so the user deletes it once rather than getting a second entry for the same database.
    private PgAdminServerOutcome CompareEntry(ListedServer server, DbConnectionInfo connection)
    {
        var isCurrent = server.Name == connection.Name && server.PasswordCommand == (PasswordCommand(connection, _appPath) ?? "");
        return isCurrent ? PgAdminServerOutcome.AlreadyThere : PgAdminServerOutcome.OutdatedEntry;
    }

    // pgAdmin's layout: runtime\pgAdmin4.exe (plus libpq.dll), python\python.exe, web\setup.py.
    private static SetupScript? FindSetup(string pgAdminExecutable)
    {
        var runtimeFolder = Path.GetDirectoryName(pgAdminExecutable);
        var installFolder = runtimeFolder is null ? null : Directory.GetParent(runtimeFolder)?.FullName;
        if (runtimeFolder is null || installFolder is null) return null;

        var python = Path.Combine(installFolder, "python", "python.exe");
        var setup = new SetupScript(python, Path.Combine(installFolder, "web", "setup.py"), runtimeFolder);
        return File.Exists(setup.Python) && File.Exists(setup.Script) ? setup : null;
    }

    // dump-servers takes 10 to 45 seconds, so its answer is reused while pgadmin4.db is unchanged. The stamp is taken
    // before the dump: a change made during it gives a newer stamp next time, so a stale list is never kept.
    private async Task<IReadOnlyList<ListedServer>?> ListServersAsync(SetupScript setup, CancellationToken cancellationToken)
    {
        var stamp = StampOfDesktopDatabase();
        var lastListing = _lastListing;
        if (stamp is not null && lastListing?.Stamp == stamp) return lastListing.Servers;

        var servers = await DumpServersAsync(setup, cancellationToken).ConfigureAwait(false);
        _lastListing = stamp is null || servers is null ? null : new ServerListing(stamp, servers);
        return servers;
    }

    // Null when there is no pgadmin4.db yet; then nothing is reused. SQLite may keep recent changes in a -wal file.
    private DatabaseStamp? StampOfDesktopDatabase()
    {
        var database = new FileInfo(_desktopDatabase);
        if (!database.Exists) return null;
        var journal = new FileInfo(_desktopDatabase + "-wal");
        return new DatabaseStamp(database.Length, database.LastWriteTimeUtc, journal.Exists ? journal.LastWriteTimeUtc : default);
    }

    private async Task<IReadOnlyList<ListedServer>?> DumpServersAsync(SetupScript setup, CancellationToken cancellationToken)
    {
        var dumpFile = TempFile();
        try
        {
            await RunSetupAsync(setup, ["dump-servers", dumpFile], cancellationToken).ConfigureAwait(false);
            if (!File.Exists(dumpFile)) return null;
            return ParseServers(await File.ReadAllTextAsync(dumpFile, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            DeleteTempFile(dumpFile);
        }
    }

    private async Task<bool> ImportAsync(SetupScript setup, DbConnectionInfo connection, CancellationToken cancellationToken)
    {
        var importFile = TempFile();
        try
        {
            await File.WriteAllTextAsync(importFile, ServersJson(connection, _appPath), cancellationToken).ConfigureAwait(false);
            var run = await RunSetupAsync(setup, ["load-servers", importFile], cancellationToken).ConfigureAwait(false);
            var added = AddedServersLine.Match(run.Output);
            return added.Success && added.Groups["servers"].Value != "0";
        }
        finally
        {
            DeleteTempFile(importFile);
        }
    }

    private Task<ProcessRun> RunSetupAsync(SetupScript setup, IReadOnlyList<string> command, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { setup.Script };
        arguments.AddRange(command);
        arguments.AddRange(["--user", DesktopUser]);
        if (File.Exists(_desktopDatabase)) arguments.AddRange(["--sqlite-path", _desktopDatabase]);
        return _processes.RunAsync(new ProcessCommand(setup.Python, arguments, setup.RuntimeFolder), SetupTimeout, cancellationToken);
    }

    // The dump is pgAdmin's own file; a value it lacks or a file it broke means "not a match", never a crash.
    private static IReadOnlyList<ListedServer>? ParseServers(string dumpJson)
    {
        try
        {
            using var document = JsonDocument.Parse(dumpJson);
            var hasServers = document.RootElement.TryGetProperty("Servers", out var servers);
            if (!hasServers || servers.ValueKind != JsonValueKind.Object) return null;
            return servers.EnumerateObject().Select(server => ToListedServer(server.Value)).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ListedServer ToListedServer(JsonElement server) => new(
        TextOf(server, "Name"),
        TextOf(server, "Group"),
        TextOf(server, "Host"),
        server.TryGetProperty("Port", out var port) && port.TryGetInt32(out var number) ? number : 0,
        TextOf(server, "Username"),
        TextOf(server, "DBRestriction"),
        TextOf(server, "PasswordExecCommand"));

    private static string TextOf(JsonElement server, string name) =>
        server.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    // Only the app's own group counts: a server the user added themselves stays theirs, and ours goes beside it.
    // One entry per database: the restriction is what tells two databases on the same login apart.
    private static bool IsSameEntry(ListedServer server, DbConnectionInfo connection)
    {
        var isSameEndpoint = server.Port == connection.Port
            && string.Equals(server.Host, connection.Host, StringComparison.OrdinalIgnoreCase);
        var isSameDatabase = server.Group == GroupName && server.Database == connection.Database;
        return isSameEndpoint && isSameDatabase && server.Username == connection.Username;
    }

    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"dbconnecthub-{Guid.NewGuid():N}.json");

    // Neither file holds a password. A file pgAdmin still has open must not turn a finished import into an error
    // that stops pgAdmin from opening, so a failed delete is skipped.
    private static void DeleteTempFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

namespace DbConnectHub.Integrations;

/// <summary>
/// Opens a database in a client with the connection already set up:
/// DBeaver connects straight away (password included); pgAdmin gets the login added under "DB Connect Hub" if it
/// isn't there yet, with a password command that runs this app, because writing the password to pgAdmin's file
/// would break the rule that passwords live only in Credential Manager; DbVisualizer is started with the URL copied.
/// </summary>
public sealed class DbToolLauncher
{
    // DBeaver and pgAdmin are slow to show a window on a cold start; the screen shows progress until then.
    private static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(60);

    private readonly DbToolLocator _locator;
    private readonly IProcessRunner _processes;
    private readonly PgAdminServers _pgAdminServers;

    /// <param name="appPath">This app's exe, which pgAdmin runs to get a password; null makes pgAdmin ask instead.</param>
    public DbToolLauncher(DbToolLocator locator, IProcessRunner processes, string? appPath)
    {
        _locator = locator;
        _processes = processes;
        _pgAdminServers = new PgAdminServers(processes, appPath);
    }

    public bool IsInstalled(DbTool tool) => _locator.Find(tool) is not null;

    public async Task<LaunchResult> OpenAsync(DbTool tool, DbConnectionInfo connection, CancellationToken cancellationToken)
    {
        var executable = _locator.Find(tool);
        if (executable is null) return new LaunchResult(false, $"{DisplayName(tool)} isn't installed on this PC.");

        return tool switch
        {
            DbTool.DBeaver => await OpenInDbeaverAsync(executable, connection, cancellationToken).ConfigureAwait(false),
            DbTool.PgAdmin => await OpenInPgAdminAsync(executable, connection, cancellationToken).ConfigureAwait(false),
            _ => await OpenInDbVisualizerAsync(executable, cancellationToken).ConfigureAwait(false),
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

    private async Task<LaunchResult> OpenInDbeaverAsync(string executable, DbConnectionInfo connection, CancellationToken cancellationToken)
    {
        var arguments = DbeaverArguments(connection);
        if (arguments is null)
            return new LaunchResult(false, "DBeaver can't take a value that contains \"|\". Copy the details into DBeaver instead.");

        var started = await StartAsync(executable, arguments, cancellationToken).ConfigureAwait(false);
        return Opened(DbTool.DBeaver, started, $"It connects to {connection.Database} by itself.");
    }

    // pgAdmin reads its server list when it starts, so the list is brought up to date before the window opens.
    private async Task<LaunchResult> OpenInPgAdminAsync(string executable, DbConnectionInfo connection, CancellationToken cancellationToken)
    {
        var outcome = await _pgAdminServers.AddIfMissingAsync(executable, connection, cancellationToken).ConfigureAwait(false);
        var started = await StartAsync(executable, [], cancellationToken).ConfigureAwait(false);

        var server = $"\"{PgAdminServers.GroupName}\" › {connection.Name}";
        var detail = outcome switch
        {
            PgAdminServerOutcome.Added => $"{server} was added, showing only {connection.Database}. Click it there to connect.",
            PgAdminServerOutcome.AlreadyThere => $"{server} was already there. Click it to connect to {connection.Database}.",
            PgAdminServerOutcome.OutdatedEntry =>
                $"pgAdmin has an older entry for {connection.Database} under \"{PgAdminServers.GroupName}\". "
                + "Delete it in pgAdmin, then click Open in pgAdmin again.",
            _ => "The server couldn't be added for you. Use the details here to add it.",
        };
        // A pgAdmin that was already running read its server list before the new server existed.
        var isRefreshNeeded = outcome == PgAdminServerOutcome.Added && started == StartOutcome.HandedOff;
        return Opened(DbTool.PgAdmin, started, isRefreshNeeded ? $"{detail} Right-click Servers in pgAdmin and choose Refresh to see it." : detail);
    }

    private async Task<LaunchResult> OpenInDbVisualizerAsync(string executable, CancellationToken cancellationToken)
    {
        var started = await StartAsync(executable, [], cancellationToken).ConfigureAwait(false);
        return Opened(DbTool.DbVisualizer, started, "The connection URL is on your clipboard: choose Create Connection and paste it.");
    }

    private Task<StartOutcome> StartAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        _processes.StartAsync(new ProcessCommand(executable, arguments), WindowTimeout, cancellationToken);

    private static LaunchResult Opened(DbTool tool, StartOutcome started, string detail)
    {
        var name = DisplayName(tool);
        var opening = started switch
        {
            StartOutcome.WindowShown => $"{name} is open.",
            StartOutcome.HandedOff => $"{name} was already open, so the request went to that window.",
            _ => $"{name} is still starting; its window will appear shortly.",
        };
        return new LaunchResult(true, $"{opening} {detail}");
    }
}

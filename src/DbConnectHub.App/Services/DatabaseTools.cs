using DbConnectHub.Integrations;
using DbConnectHub.Presentation;

namespace DbConnectHub.App.Services;

/// <summary>Connects the screen's "Open in…" buttons to the Integrations launcher.</summary>
public sealed class DatabaseTools : IDatabaseTools
{
    private readonly DbToolLauncher _launcher = CreateLauncher();

    public bool IsInstalled(DatabaseTool tool) => _launcher.IsInstalled(ToDbTool(tool));

    public async Task<ToolLaunch> OpenAsync(DatabaseTool tool, DatabaseLaunchRequest request)
    {
        try
        {
            var connection = new DbConnectionInfo(request.Name, request.Host, request.Port, request.Database, request.Username, request.Password);
            var result = await _launcher.OpenAsync(ToDbTool(tool), connection, CancellationToken.None);
            return new ToolLaunch(result.Success, result.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new ToolLaunch(false, $"{DbToolLauncher.DisplayName(ToDbTool(tool))} couldn't be started: {exception.Message}");
        }
    }

    private static DbTool ToDbTool(DatabaseTool tool) => tool switch
    {
        DatabaseTool.DBeaver => DbTool.DBeaver,
        DatabaseTool.PgAdmin => DbTool.PgAdmin,
        _ => DbTool.DbVisualizer,
    };

    // pgAdmin runs this exe (Environment.ProcessPath) to get a password when the user connects to a server.
    private static DbToolLauncher CreateLauncher()
    {
        var processes = new ProcessRunner();
        var pgAdminServers = new PgAdminServers(processes, Environment.ProcessPath, PgAdminServers.DesktopDatabase);
        return new DbToolLauncher(new DbToolLocator(DbToolLocator.DefaultInstallRoots()), processes, pgAdminServers);
    }
}

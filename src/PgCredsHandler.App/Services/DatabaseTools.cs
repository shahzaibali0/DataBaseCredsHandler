using PgCredsHandler.Integrations;
using PgCredsHandler.Presentation;

namespace PgCredsHandler.App.Services;

/// <summary>Connects the screen's "Open in…" buttons to the Integrations launcher.</summary>
public sealed class DatabaseTools : IDatabaseTools
{
    private readonly DbToolLauncher _launcher = new(new DbToolLocator(DbToolLocator.DefaultInstallRoots()), new ProcessRunner());

    public bool IsInstalled(DatabaseTool tool) => _launcher.IsInstalled(ToDbTool(tool));

    public ToolLaunch Open(DatabaseTool tool, DatabaseLaunchRequest request)
    {
        try
        {
            var result = _launcher.Open(
                ToDbTool(tool),
                new DbConnectionInfo(request.Name, request.Host, request.Port, request.Database, request.Username, request.Password));
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
}

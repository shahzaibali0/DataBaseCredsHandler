using CommunityToolkit.Mvvm.Input;
using DbConnectHub.Core.Abstractions;
using DbConnectHub.Scanner;

namespace DbConnectHub.Presentation;

/// <summary>The Ports section's actions: run an app's dev server, and change the port it runs on.</summary>
public sealed partial class MainViewModel
{
    // A dev server takes a few seconds to open its port; the dot is checked then instead of at the next 15-second tick.
    private static readonly TimeSpan ServerStartCheckDelay = TimeSpan.FromSeconds(6);

    /// <summary>What changing the port would do, shown in the port dialog before the user confirms.</summary>
    public static PortChange PreviewPortChange(AppGroupViewModel app, int newPort) => ServerPorts.Preview(app.App, newPort);

    /// <summary>Changes the port in the app's file, unless another program uses it right now, then shows the new address.</summary>
    public async Task ChangePortAsync(AppGroupViewModel app, int newPort)
    {
        if (await _services.Ports.IsListeningAsync(newPort, CancellationToken.None))
        {
            ShowNotice($"Port {newPort} is in use by another program right now. Choose another one.", true);
            return;
        }

        try
        {
            var change = new ServerPorts(_services.Stores.Inventory).Change(app.App, newPort);
            if (change.IsPossible) ShowSaved();
            ShowNotice(change.Message, !change.IsPossible);
        }
        catch (DataFileUnreadableException exception)
        {
            ShowUnreadableData(exception);
        }
    }

    [RelayCommand]
    private void RunServer(AppGroupViewModel app)
    {
        if (app.RunState == RunState.Running)
        {
            ShowNotice($"{app.Title} is already running at {app.LocalUrl}.", false);
            return;
        }

        var command = ServerCommands.Find(app.App);
        if (command is null)
        {
            ShowNotice($"{app.Title} has no dev or start script to run.", true);
            return;
        }

        var launch = _services.Servers.Start(command);
        ShowNotice(launch.Message, !launch.Success);
        if (launch.Success) _ = RefreshStatusSoonAsync();
    }

    private async Task RefreshStatusSoonAsync()
    {
        await Task.Delay(ServerStartCheckDelay);
        await RefreshStatusQuietlyAsync();
    }
}

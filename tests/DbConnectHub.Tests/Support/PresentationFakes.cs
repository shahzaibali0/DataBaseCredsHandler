using DbConnectHub.Presentation;
using DbConnectHub.Scanner;

namespace DbConnectHub.Tests.Support;

public sealed class FakeDesktopShell : IDesktopShell
{
    public string? FolderToPick { get; set; }

    public List<string> OpenedUrls { get; } = [];

    public string DataFolderPath => @"C:\Users\you\AppData\Roaming\DbConnectHub";

    public void OpenFolder(string folderPath) { }

    public void OpenUrl(string url) => OpenedUrls.Add(url);

    public Task<string?> PickFolderAsync() => Task.FromResult(FolderToPick);
}

public sealed class FakeStartup : IStartupRegistration
{
    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
}

public sealed class FakeServerRunner : IServerRunner
{
    public List<ServerCommand> Started { get; } = [];

    public ToolLaunch Start(ServerCommand command)
    {
        Started.Add(command);
        return new ToolLaunch(true, "started");
    }
}

using System.Diagnostics;
using DbConnectHub.Presentation;
using DbConnectHub.Storage;
using Windows.Storage.Pickers;

namespace DbConnectHub.App.Services;

/// <summary>Windows shell actions for the viewmodels: the data folder, Explorer, the browser and the folder picker.</summary>
public sealed class DesktopShell : IDesktopShell
{
    private readonly Func<IntPtr> _getWindowHandle;

    /// <param name="getWindowHandle">The main window's handle; an unpackaged app must give it to every picker.</param>
    public DesktopShell(Func<IntPtr> getWindowHandle) => _getWindowHandle = getWindowHandle;

    public string DataFolderPath => Paths.DataFolder;

    public void OpenFolder(string folderPath)
    {
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add(folderPath);
        Process.Start(startInfo);
    }

    // Only local dev-server addresses are opened; anything else is ignored rather than handed to the shell.
    public void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !uri.IsLoopback) return;
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _getWindowHandle());
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}

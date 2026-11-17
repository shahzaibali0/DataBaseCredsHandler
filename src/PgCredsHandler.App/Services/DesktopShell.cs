using System.Diagnostics;
using PgCredsHandler.Presentation;
using PgCredsHandler.Storage;
using Windows.Storage.Pickers;

namespace PgCredsHandler.App.Services;

/// <summary>Windows shell actions for the viewmodels: the data folder, Explorer and the folder picker.</summary>
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

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _getWindowHandle());
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}

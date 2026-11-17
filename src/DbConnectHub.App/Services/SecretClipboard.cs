using Microsoft.UI.Dispatching;
using DbConnectHub.Presentation;
using Windows.ApplicationModel.DataTransfer;

namespace DbConnectHub.App.Services;

/// <summary>
/// The Windows clipboard. A copied password is kept out of clipboard history and cloud sync, and cleared after
/// <see cref="ClearAfter"/> unless the user has copied something else since.
/// </summary>
public sealed class SecretClipboard : IClipboard
{
    private static readonly TimeSpan ClearAfter = TimeSpan.FromSeconds(30);

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private DispatcherQueueTimer? _clearTimer;

    public void CopyText(string text, bool isSecret)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContentWithOptions(package, new ClipboardContentOptions { IsAllowedInHistory = !isSecret, IsRoamable = !isSecret });
        if (isSecret) ScheduleClear(text);
    }

    private void ScheduleClear(string copied)
    {
        _clearTimer?.Stop();
        _clearTimer = _dispatcher.CreateTimer();
        _clearTimer.Interval = ClearAfter;
        _clearTimer.IsRepeating = false;
        _clearTimer.Tick += async (timer, _) =>
        {
            timer.Stop();
            try
            {
                var content = Clipboard.GetContent();
                if (content.Contains(StandardDataFormats.Text) && await content.GetTextAsync() == copied) Clipboard.Clear();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // the clipboard was busy; the next copy replaces it anyway
            }
        };
        _clearTimer.Start();
    }
}

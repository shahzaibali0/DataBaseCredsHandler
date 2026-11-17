using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

namespace PgCredsHandler.App.Services;

/// <summary>What the tray icon's menu can do.</summary>
public sealed record TrayActions(Action Show, Action Rescan, Action Exit);

/// <summary>The notification-area icon: click to open, right-click for Open / Rescan / Exit.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayIcon(TrayActions actions)
    {
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Open PgCreds Handler", Command = new RelayCommand(actions.Show) });
        menu.Items.Add(new MenuFlyoutItem { Text = "Rescan now", Command = new RelayCommand(actions.Rescan) });
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(new MenuFlyoutItem { Text = "Exit", Command = new RelayCommand(actions.Exit) });

        _icon = new TaskbarIcon
        {
            ToolTipText = "PgCreds Handler: your projects and databases, on this PC only",
            ContextFlyout = menu,
            ContextMenuMode = ContextMenuMode.SecondWindow,
            LeftClickCommand = new RelayCommand(actions.Show),
            NoLeftClickDelay = true,
        };
        var appIcon = AppIconLoader.Load(large: false);
        if (appIcon != IntPtr.Zero) _icon.Icon = System.Drawing.Icon.FromHandle(appIcon);
        _icon.ForceCreate();
    }

    public void Dispose() => _icon.Dispose();
}

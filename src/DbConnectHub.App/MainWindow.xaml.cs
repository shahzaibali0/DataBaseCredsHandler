using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using DbConnectHub.App.Pages;
using DbConnectHub.Presentation;
using WinUIEx;

namespace DbConnectHub.App;

/// <summary>
/// The window: title bar, sidebar navigation and the page frame. Closing it hides it to the tray (unless the
/// user turned that off); every shown password is hidden again first, and the window's memory goes back to Windows.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int StartWidth = 1240;
    private const int StartHeight = 820;
    private const int MinimumWidth = 900;
    private const int MinimumHeight = 620;

    private static readonly Dictionary<string, Type> PagesByTag = new(StringComparer.Ordinal)
    {
        ["Databases"] = typeof(DatabasesPage),
        ["Privacy"] = typeof(PrivacyPage),
        ["Settings"] = typeof(SettingsPage),
    };

    private readonly MainViewModel _viewModel;
    private readonly Action _exitApp;
    private bool _isExiting;

    public MainWindow(MainViewModel viewModel, Action exitApp)
    {
        _viewModel = viewModel;
        _exitApp = exitApp;
        InitializeComponent();

        var appIcon = Services.AppIconLoader.Load(large: true);
        if (appIcon != IntPtr.Zero) AppWindow.SetIcon(Microsoft.UI.Win32Interop.GetIconIdFromIcon(appIcon));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var windowManager = WindowManager.Get(this);
        windowManager.MinWidth = MinimumWidth;
        windowManager.MinHeight = MinimumHeight;
        this.SetWindowSize(StartWidth, StartHeight);
        this.CenterOnScreen();
        AppWindow.Closing += OnClosing;

        Navigation.SelectedItem = Navigation.MenuItems[0];
    }

    public void ShowAndFocus()
    {
        AppWindow.Show();
        Activate();
        this.SetForegroundWindow();
    }

    public void CloseForExit()
    {
        _isExiting = true;
        Close();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting) return;

        if (!_viewModel.KeepRunningInTray)
        {
            args.Cancel = true;
            _exitApp();
            return;
        }

        args.Cancel = true;
        _viewModel.HideAllPasswords();
        sender.Hide();
        // Hidden, not closed: WinUI doesn't free all of a closed window, so building a new one on every open would
        // grow the app each time. Once the hide has been drawn, the memory the screen let go of goes back to Windows.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, Services.TrayMemory.Release);
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag }) return;
        if (!PagesByTag.TryGetValue(tag, out var pageType) || ContentFrame.CurrentSourcePageType == pageType) return;

        ContentFrame.Navigate(pageType, _viewModel, new EntranceNavigationTransitionInfo());
    }
}

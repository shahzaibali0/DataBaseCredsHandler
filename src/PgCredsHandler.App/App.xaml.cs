using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PgCredsHandler.App.Services;
using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Presentation;
using PgCredsHandler.Scanner;
using PgCredsHandler.Storage;

namespace PgCredsHandler.App;

/// <summary>
/// Composition root, and the app's life in the background: one instance per user, a tray icon, closing to the
/// tray, starting hidden when Windows starts it (<c>--background</c>), and a quiet rescan on startup.
/// </summary>
public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;
    private TrayIcon? _trayIcon;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.SignalRunningInstance();
            Exit();
            return;
        }

        _viewModel = CreateViewModel();
        _mainWindow = new MainWindow(_viewModel, ExitApp);
        _trayIcon = new TrayIcon(new TrayActions(ShowMainWindow, () => _viewModel.ScanCommand.Execute(null), ExitApp));

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _singleInstance.ListenForShowRequests(() => dispatcher.TryEnqueue(ShowMainWindow));

        if (!Environment.GetCommandLineArgs().Contains(StartupRegistration.BackgroundArgument)) ShowMainWindow();
        if (_viewModel.ShouldScanOnStartup) _viewModel.ScanCommand.Execute(null);
    }

    private MainViewModel CreateViewModel()
    {
        Func<IntPtr> windowHandle = () => _mainWindow is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
        var stores = new DataStores(
            new JsonConnectionRepository(), CredentialStoreFactory.Create(), new JsonSettingsStore(), new JsonInventoryRepository());
        var services = new AppServices(
            new ScanWorkflow(stores, LocalDrives.ListFixedDriveRoots), stores, new DesktopShell(windowHandle), new StartupRegistration(),
            new DatabaseActions(stores.Credentials, new DatabaseTools(), new SecretClipboard()));

        var viewModel = new MainViewModel(services);
        viewModel.Load();
        return viewModel;
    }

    private void ShowMainWindow() => _mainWindow?.ShowAndFocus();

    private void ExitApp()
    {
        _viewModel?.HideAllPasswords();
        _trayIcon?.Dispose();
        _mainWindow?.CloseForExit();
        _singleInstance?.Dispose();
        Exit();
    }
}

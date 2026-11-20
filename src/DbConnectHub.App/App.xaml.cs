using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using DbConnectHub.App.Services;
using DbConnectHub.Core.Abstractions;
using DbConnectHub.Presentation;
using DbConnectHub.Scanner;
using DbConnectHub.Storage;

namespace DbConnectHub.App;

/// <summary>
/// Composition root, and the app's life in the background: one instance per user, a tray icon, closing to the
/// tray, starting hidden when Windows starts it (<c>--background</c>), and a quiet rescan on startup. The window
/// is built the first time the user opens it, so a start in the background never pays for it.
/// </summary>
public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;
    private TrayIcon? _trayIcon;
    private readonly CancellationTokenSource _stopping = new();

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
        _trayIcon = new TrayIcon(new TrayActions(ShowMainWindow, () => _viewModel.ScanCommand.Execute(null), ExitApp));

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _singleInstance.ListenForShowRequests(() => dispatcher.TryEnqueue(ShowMainWindow));

        var isBackgroundStart = Environment.GetCommandLineArgs().Contains(StartupRegistration.BackgroundArgument);
        if (!isBackgroundStart) ShowMainWindow();
        _ = ScanOnStartupAsync(_viewModel, isBackgroundStart);
        _ = _viewModel.KeepStatusFreshAsync(_stopping.Token);
    }

    // Started by Windows into the tray: no window ever showed, so nothing else gives the startup scan's memory back.
    private async Task ScanOnStartupAsync(MainViewModel viewModel, bool isBackgroundStart)
    {
        if (viewModel.ShouldScanOnStartup) await viewModel.ScanCommand.ExecuteAsync(null);
        if (isBackgroundStart && _mainWindow is null) TrayMemory.Release();
    }

    private MainViewModel CreateViewModel()
    {
        Func<IntPtr> windowHandle = () => _mainWindow is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
        MoveDataFromOldName();
        var stores = new DataStores(
            new JsonConnectionRepository(), CredentialStoreFactory.Create(), new JsonSettingsStore(), new JsonInventoryRepository());
        var services = new AppServices(
            new ScanWorkflow(stores, LocalDrives.ListFixedDriveRoots), stores, new DesktopShell(windowHandle), new StartupRegistration(),
            new DatabaseActions(stores.Credentials, new DatabaseTools(), new SecretClipboard()), new LocalPorts(), new ServerRunner());

        var viewModel = new MainViewModel(services);
        viewModel.Load();
        return viewModel;
    }

    // The app used to be "PgCreds Handler": bring the user's saved list and passwords across on first start.
    // Skipped when a custom data folder is set, so a demo or test folder never touches the real data.
    private static void MoveDataFromOldName()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Paths.DataFolderVariable))) return;
        try
        {
            LegacyDataMigration.Run(
                LegacyDataMigration.DefaultLegacyFolder(), Paths.DataFolder,
                new WindowsCredentialStore(LegacyDataMigration.LegacyName), new WindowsCredentialStore());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // The old data stays where it is; the next start tries again.
        }
    }

    private void ShowMainWindow()
    {
        if (_viewModel is null) return;
        _mainWindow ??= new MainWindow(_viewModel, ExitApp);
        _mainWindow.ShowAndFocus();
    }

    private void ExitApp()
    {
        _stopping.Cancel();
        _viewModel?.HideAllPasswords();
        _trayIcon?.Dispose();
        _mainWindow?.CloseForExit();
        _singleInstance?.Dispose();
        Exit();
    }
}

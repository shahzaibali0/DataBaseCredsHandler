using Microsoft.UI.Xaml;
using PgCredsHandler.App.Services;
using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Presentation;
using PgCredsHandler.Scanner;
using PgCredsHandler.Storage;

namespace PgCredsHandler.App;

/// <summary>Composition root: the only place that creates the concrete stores and services.</summary>
public partial class App : Application
{
    private MainWindow? _mainWindow;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var stores = new DataStores(new JsonConnectionRepository(), CredentialStoreFactory.Create(), new JsonSettingsStore());
        var scanWorkflow = new ScanWorkflow(stores, LocalDrives.ListFixedDriveRoots);
        var shell = new DesktopShell(() => _mainWindow is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow));
        var viewModel = new MainViewModel(scanWorkflow, stores, shell);
        viewModel.Load();

        _mainWindow = new MainWindow(viewModel);
        _mainWindow.Activate();
    }
}

using DbConnectHub.App.Services;

namespace DbConnectHub.App;

/// <summary>
/// The entry point, written by hand (DISABLE_XAML_GENERATED_MAIN) so pgAdmin's password command can be answered
/// before any window, tray icon or single-instance check: that run prints one password and exits.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (PgAdminPasswordCommand.IsRequested(args)) return PgAdminPasswordCommand.Run(args);

        // The same start-up the XAML compiler generates.
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(startParams =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }
}

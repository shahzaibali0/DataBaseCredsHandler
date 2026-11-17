using Microsoft.Win32;
using DbConnectHub.Presentation;

namespace DbConnectHub.App.Services;

/// <summary>
/// Starts the app in the tray when the user signs in to Windows, through the per-user Run key
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). No admin rights, and Task Manager → Startup apps shows it.
/// </summary>
public sealed class StartupRegistration : IStartupRegistration
{
    public const string BackgroundArgument = "--background";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DB Connect Hub";
    private const string LegacyValueName = "PgCreds Handler";

    public bool IsEnabled
    {
        get
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return runKey?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool isEnabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        runKey.DeleteValue(LegacyValueName, throwOnMissingValue: false); // the entry from before the rename
        if (isEnabled && Environment.ProcessPath is { } exePath)
            runKey.SetValue(ValueName, $"\"{exePath}\" {BackgroundArgument}");
        else
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

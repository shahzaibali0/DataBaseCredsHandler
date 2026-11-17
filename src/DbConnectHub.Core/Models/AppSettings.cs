namespace DbConnectHub.Core.Models;

public sealed class AppSettings
{
    /// <summary>
    /// Folders the scanner walks, e.g. C:\Users\you\Projects. Empty (the default) means every fixed
    /// local drive; USB, network and CD drives are never scanned unless chosen here.
    /// </summary>
    public List<string> ScanRoots { get; set; } = new();

    /// <summary>Full path to dbeaver.exe. Empty = auto-detect.</summary>
    public string? DbeaverPath { get; set; }

    /// <summary>Rescan quietly when the app starts, once the user has scanned at least once.</summary>
    public bool ScanOnStartup { get; set; } = true;

    /// <summary>Start in the tray when Windows starts, so the list is always one click away.</summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>Closing the window keeps the app running in the tray instead of quitting.</summary>
    public bool KeepRunningInTray { get; set; } = true;

    /// <summary>Folder names the scanner never enters.</summary>
    public List<string> ExcludedFolderNames { get; set; } = DefaultExcludedFolders();

    /// <summary>Fingerprints of scanned connections the user deleted, so rescans don't bring them back.</summary>
    public List<string> HiddenFingerprints { get; set; } = new();

    public static List<string> DefaultExcludedFolders() => new()
    {
        "node_modules", ".git", ".svn", ".hg", "bin", "obj", "dist", "build", "out", "target",
        ".next", ".nuxt", ".turbo", ".cache", ".parcel-cache", "coverage",
        "venv", ".venv", "__pycache__", ".tox", ".mypy_cache", ".pytest_cache", "site-packages",
        "vendor", "packages", ".nuget", ".gradle", ".idea", ".vs", ".vscode",
        "$RECYCLE.BIN", "System Volume Information", "Windows", "Program Files", "Program Files (x86)",
        "ProgramData", "AppData", "Windows.old", "Recovery", "PerfLogs", "Config.Msi", "OneDriveTemp"
    };
}

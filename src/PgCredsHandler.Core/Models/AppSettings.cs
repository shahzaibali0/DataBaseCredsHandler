namespace PgCredsHandler.Core.Models;

public sealed class AppSettings
{
    /// <summary>Folders the scanner walks, e.g. D:\Shahzaib.</summary>
    public List<string> ScanRoots { get; set; } = new();

    /// <summary>Full path to dbeaver.exe. Empty = auto-detect.</summary>
    public string? DbeaverPath { get; set; }

    /// <summary>Ask for Windows Hello before revealing or copying a password.</summary>
    public bool RequireWindowsHello { get; set; } = true;

    /// <summary>After a successful Windows Hello check, stay unlocked this long.</summary>
    public int UnlockMinutes { get; set; } = 5;

    /// <summary>A revealed password hides itself again after this many seconds.</summary>
    public int RevealSeconds { get; set; } = 30;

    /// <summary>A copied password is cleared from the clipboard after this many seconds.</summary>
    public int ClipboardClearSeconds { get; set; } = 30;

    public bool ScanOnStartup { get; set; } = true;

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
        "ProgramData", "AppData"
    };
}

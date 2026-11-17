namespace PgCredsHandler.Presentation;

/// <summary>The few Windows shell actions a screen needs, behind an interface so viewmodels stay testable.</summary>
public interface IDesktopShell
{
    /// <summary>The folder that holds this user's data (created on first use).</summary>
    string DataFolderPath { get; }

    void OpenFolder(string folderPath);

    /// <summary>Opens a local address (an app's dev server) in the default browser.</summary>
    void OpenUrl(string url);

    /// <summary>Lets the user pick a folder; null when they cancel.</summary>
    Task<string?> PickFolderAsync();
}

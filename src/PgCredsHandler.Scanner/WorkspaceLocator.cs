namespace PgCredsHandler.Scanner;

/// <summary>
/// Finds the project an app belongs to: the nearest folder at or above the app that holds a .git folder or file,
/// never above the scan root. A backend and a frontend in one repository then show as one project.
/// </summary>
public sealed class WorkspaceLocator
{
    private readonly Dictionary<string, string?> _gitRootByFolder = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The project folder for an app; the app folder itself when no repository is found.</summary>
    public string FindWorkspacePath(string appPath, string scanRoot) =>
        FindGitRoot(Path.GetFullPath(appPath), Path.GetFullPath(scanRoot)) ?? appPath;

    public static string NameOf(string folderPath) => Path.GetFileName(folderPath.TrimEnd('/', '\\')) is { Length: > 0 } name
        ? name
        : folderPath;

    private string? FindGitRoot(string folder, string scanRoot)
    {
        if (_gitRootByFolder.TryGetValue(folder, out var cached)) return cached;

        var gitRoot = HasGit(folder) ? folder : FindInParent(folder, scanRoot);
        _gitRootByFolder[folder] = gitRoot;
        return gitRoot;
    }

    private string? FindInParent(string folder, string scanRoot)
    {
        var parent = Path.GetDirectoryName(folder);
        var isAtOrAboveRoot = parent is null || folder.Length <= scanRoot.TrimEnd('\\', '/').Length;
        return isAtOrAboveRoot ? null : FindGitRoot(parent!, scanRoot);
    }

    private static bool HasGit(string folder)
    {
        var gitPath = Path.Combine(folder, ".git");
        try { return Directory.Exists(gitPath) || File.Exists(gitPath); }
        catch (IOException) { return false; } // a folder we can't read is simply not a repository
    }
}

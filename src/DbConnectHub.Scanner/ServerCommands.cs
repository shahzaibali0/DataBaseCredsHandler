using DbConnectHub.Core.Models;

namespace DbConnectHub.Scanner;

/// <summary>
/// The command that starts an app's dev server, run in <paramref name="Folder"/>. Every word comes from a fixed list
/// in this file, never from a project file, because it is run through cmd.exe.
/// </summary>
public sealed record ServerCommand(string Program, IReadOnlyList<string> Arguments, string Folder)
{
    /// <summary>"pnpm run dev", for the screen.</summary>
    public string Text => string.Join(' ', [Program, .. Arguments]);
}

/// <summary>Works out how a project starts its dev server: its package manager and script, or <c>dotnet run</c>.</summary>
public static class ServerCommands
{
    // The lock file shows which package manager the project uses; npm when there is none.
    private static readonly (string LockFile, string Program)[] PackageManagers =
    [
        ("pnpm-lock.yaml", "pnpm"),
        ("yarn.lock", "yarn"),
        ("bun.lockb", "bun"),
        ("bun.lock", "bun"),
    ];

    /// <summary>The start command, or null when the app has no dev server script or its folder is gone.</summary>
    public static ServerCommand? Find(SavedApp app)
    {
        if (!Directory.Exists(app.AppPath)) return null;
        if (app.Framework == AppDetector.AspNetCore) return new ServerCommand("dotnet", ["run"], app.AppPath);

        using var package = AppDetector.TryReadPackageJson(Path.Combine(app.AppPath, "package.json"));
        var scriptName = package is null ? null : AppDetector.ServerScriptName(package.RootElement);
        return scriptName is null ? null : new ServerCommand(PackageManagerOf(app), ["run", scriptName], app.AppPath);
    }

    // A monorepo keeps one lock file at its root, so the folders from the app up to its project are checked.
    private static string PackageManagerOf(SavedApp app)
    {
        for (var folder = new DirectoryInfo(app.AppPath); folder is not null; folder = folder.Parent)
        {
            var manager = PackageManagers.FirstOrDefault(candidate => File.Exists(Path.Combine(folder.FullName, candidate.LockFile)));
            if (manager.Program is not null) return manager.Program;
            if (File.Exists(Path.Combine(folder.FullName, "package-lock.json"))) return "npm";
            if (IsSameFolder(folder.FullName, app.WorkspacePath)) break;
        }
        return "npm";
    }

    private static bool IsSameFolder(string folder, string other) =>
        string.Equals(Path.TrimEndingDirectorySeparator(folder), Path.TrimEndingDirectorySeparator(other), StringComparison.OrdinalIgnoreCase);
}

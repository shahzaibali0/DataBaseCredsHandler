namespace PgCredsHandler.Core.Models;

/// <summary>What an app is for, guessed from its folder name and framework.</summary>
public enum AppRole
{
    Backend,
    Frontend,
    App,
}

/// <summary>
/// An app found by the scanner: a folder with a package.json or .csproj, inside a project (its git repository).
/// Not secret, so it is stored as found and replaced on every scan of its root.
/// </summary>
public sealed class SavedApp
{
    /// <summary>The project the app belongs to: the nearest folder holding .git, or the app folder itself.</summary>
    public string WorkspaceName { get; set; } = "";

    public string WorkspacePath { get; set; } = "";

    public string AppName { get; set; } = "";

    public string AppPath { get; set; } = "";

    public AppRole Role { get; set; } = AppRole.App;

    /// <summary>"Next.js", "Vite", "NestJS", "ASP.NET Core"… or empty when unknown.</summary>
    public string Framework { get; set; } = "";

    /// <summary>Where the dev server runs, e.g. http://localhost:3001; null when it can't be told.</summary>
    public string? LocalUrl { get; set; }

    /// <summary>Where the URL came from, e.g. "package.json dev script", ".env PORT", "Next.js default".</summary>
    public string? LocalUrlSource { get; set; }

    /// <summary>Framework, UI library, language and runtime, e.g. "Next.js 16", "React 19", "TypeScript".</summary>
    public List<string> Stack { get; set; } = [];

    /// <summary>The notable third-party packages, by purpose (not every dependency).</summary>
    public List<KeyPackage> Packages { get; set; } = [];
}

/// <summary>One notable package: what it is for ("Database", "Auth", "Payments"…) and its common name.</summary>
public sealed record KeyPackage(string Category, string Name);

/// <summary>Everything a scan knows about apps, kept in one file.</summary>
public sealed class InventorySnapshot
{
    public List<SavedApp> Apps { get; set; } = [];
}

/// <summary>An app as the scanner found it.</summary>
public sealed record DiscoveredApp(
    string WorkspaceName, string WorkspacePath, string AppName, string AppPath,
    AppRole Role, string Framework, string? LocalUrl, string? LocalUrlSource)
{
    public IReadOnlyList<string> Stack { get; init; } = [];

    public IReadOnlyList<KeyPackage> Packages { get; init; } = [];
}

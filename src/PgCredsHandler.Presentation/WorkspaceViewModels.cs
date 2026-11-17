using CommunityToolkit.Mvvm.ComponentModel;
using PgCredsHandler.Core.Models;

namespace PgCredsHandler.Presentation;

/// <summary>One app inside a project: its role, framework, local server and databases.</summary>
public sealed partial class AppGroupViewModel : ObservableObject
{
    public AppGroupViewModel(SavedApp app, IReadOnlyList<ConnectionRowViewModel> databases)
    {
        App = app;
        Databases = databases;
    }

    public SavedApp App { get; }

    public string AppName => App.AppName;

    public string AppPath => App.AppPath;

    public AppRole Role => App.Role;

    public string RoleLabel => Role switch
    {
        AppRole.Backend => "Backend",
        AppRole.Frontend => "Frontend",
        _ => "App",
    };

    public string Title => $"{RoleLabel} · {AppName}";

    public string Framework => string.IsNullOrEmpty(App.Framework) ? "Not detected" : App.Framework;

    public string? LocalUrl => App.LocalUrl;

    public bool HasLocalUrl => !string.IsNullOrEmpty(App.LocalUrl);

    public string LocalUrlDisplay => App.LocalUrl ?? "No local port detected";

    /// <summary>":3001", for the small project card.</summary>
    public string PortLabel => Uri.TryCreate(App.LocalUrl, UriKind.Absolute, out var url) ? $":{url.Port}" : "—";

    public string LocalUrlSource => App.LocalUrlSource is null ? "Port not detected" : $"From {App.LocalUrlSource}";

    public IReadOnlyList<ConnectionRowViewModel> Databases { get; }

    public bool HasDatabases => Databases.Count > 0;

    /// <summary>The dev server's port on this PC, or null when it isn't a localhost address.</summary>
    public int? LocalPort => Uri.TryCreate(App.LocalUrl, UriKind.Absolute, out var url) && url.IsLoopback ? url.Port : null;

    /// <summary>Whether the dev server is running right now.</summary>
    [ObservableProperty]
    private RunState _runState;
}

/// <summary>The notable packages of one purpose, e.g. "Database: Prisma, Redis".</summary>
public sealed record PackageGroupViewModel(string Category, string Names);

/// <summary>
/// One project (a git repository). The square card shows its name, stack, ports and counts; the detail window
/// shows its ports, every database connection, its stack and its key packages.
/// </summary>
public sealed class WorkspaceViewModel
{
    public WorkspaceViewModel(string name, string path, IReadOnlyList<AppGroupViewModel> apps)
    {
        Name = name;
        Path = path;
        Apps = apps;
        Databases = apps.SelectMany(app => app.Databases).DistinctBy(database => database.Id).ToList();
        Stack = apps.SelectMany(app => app.App.Stack).Distinct(StringComparer.Ordinal).ToList();
        Packages = apps
            .SelectMany(app => app.App.Packages)
            .Distinct()
            .GroupBy(package => package.Category, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new PackageGroupViewModel(group.Key, string.Join(", ", group.Select(package => package.Name).Distinct())))
            .ToList();
    }

    public string Name { get; }

    public string Path { get; }

    /// <summary>
    /// Two letters for the card's badge: "ShopRepo" → "SR", "acme-store" → "AS", "backend" → "Ba".
    /// </summary>
    public string Initials
    {
        get
        {
            var words = System.Text.RegularExpressions.Regex.Matches(Name, @"[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])")
                .Select(match => match.Value).ToList();
            if (words.Count >= 2) return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
            var word = words.FirstOrDefault() ?? Name;
            return word.Length switch
            {
                0 => "?",
                1 => word.ToUpperInvariant(),
                _ => $"{char.ToUpperInvariant(word[0])}{word[1]}",
            };
        }
    }

    public IReadOnlyList<AppGroupViewModel> Apps { get; }

    /// <summary>Apps with a local server, backend first: the Ports section.</summary>
    public IReadOnlyList<AppGroupViewModel> Servers => Apps.Where(app => app.HasLocalUrl).ToList();

    public IReadOnlyList<ConnectionRowViewModel> Databases { get; }

    public IReadOnlyList<string> Stack { get; }

    /// <summary>"Next.js 16 · React 19 · TypeScript", for the card.</summary>
    public string StackSummary => Stack.Count == 0 ? "Stack not detected" : string.Join(" · ", Stack.Take(3));

    public IReadOnlyList<PackageGroupViewModel> Packages { get; }

    public bool HasServers => Servers.Count > 0;

    public bool HasDatabases => Databases.Count > 0;

    public bool HasStack => Stack.Count > 0;

    public bool HasPackages => Packages.Count > 0;

    public string CountLabel => $"{Plural(Apps.Count, "app")} · {Plural(Databases.Count, "database")}";

    /// <summary>Hides every shown password in this project.</summary>
    public void HidePasswords()
    {
        foreach (var database in Databases) database.Hide();
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}

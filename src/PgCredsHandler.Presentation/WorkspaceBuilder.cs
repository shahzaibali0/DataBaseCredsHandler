using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Util;

namespace PgCredsHandler.Presentation;

/// <summary>What the Databases page can show.</summary>
public sealed record WorkspaceSources(
    IReadOnlyList<SavedApp> Apps,
    IReadOnlyList<ConnectionRowViewModel> Databases);

/// <summary>The search text and which kind of item to show (<see cref="WorkspaceBuilder.ShowAll"/> and friends).</summary>
public sealed record WorkspaceFilter(string SearchText);

/// <summary>
/// Arranges everything as Project → App → databases. A database belongs to the app whose folder holds its config
/// file; one outside every app (a docker-compose at the repository root) gets its own group
/// inside the project that contains it.
/// </summary>
public static class WorkspaceBuilder
{
    public static IReadOnlyList<WorkspaceViewModel> Build(WorkspaceSources sources, WorkspaceFilter filter)
    {
        var apps = AppsIncludingLooseFolders(sources);
        return apps
            .Select(app => ToAppGroup(app, sources, filter))
            .Where(group => group.HasDatabases || ShowsEmptyApp(group, filter.SearchText))
            .GroupBy(group => group.App.WorkspacePath, StringComparer.OrdinalIgnoreCase)
            .Select(ToWorkspace)
            .OrderBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // A config folder with no package.json/.csproj still needs a group, inside the project that contains it.
    private static List<SavedApp> AppsIncludingLooseFolders(WorkspaceSources sources)
    {
        var apps = sources.Apps.ToList();
        var itemFolders = sources.Databases.Select(row => row.ProjectPath);

        foreach (var folder in itemFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (apps.Any(app => SamePath(app.AppPath, folder))) continue;
            var workspace = apps.FirstOrDefault(app => ConnectionKey.IsUnder(folder, app.WorkspacePath));
            apps.Add(LooseFolder(folder, workspace));
        }
        return apps;
    }

    private static SavedApp LooseFolder(string folder, SavedApp? sameWorkspace) => new()
    {
        AppPath = folder,
        AppName = WorkspaceName(folder),
        WorkspacePath = sameWorkspace?.WorkspacePath ?? folder,
        WorkspaceName = sameWorkspace?.WorkspaceName ?? WorkspaceName(folder),
        Role = AppRole.App,
    };

    private static AppGroupViewModel ToAppGroup(SavedApp app, WorkspaceSources sources, WorkspaceFilter filter)
    {
        var showsWholeApp = MatchesApp(app, filter.SearchText);
        var databases = sources.Databases
            .Where(row => SamePath(row.ProjectPath, app.AppPath) && (showsWholeApp || row.Matches(filter.SearchText)))
            .ToList();

        return new AppGroupViewModel(app, databases);
    }

    // An app without databases still shows its local server.
    private static bool ShowsEmptyApp(AppGroupViewModel group, string searchText) =>
        group.HasLocalUrl && MatchesApp(group.App, searchText);

    private static bool MatchesApp(SavedApp app, string searchText) =>
        string.IsNullOrWhiteSpace(searchText)
        || new[] { app.WorkspaceName, app.AppName, app.Framework }
            .Any(field => field.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase));

    private static WorkspaceViewModel ToWorkspace(IGrouping<string, AppGroupViewModel> groups)
    {
        var first = groups.First().App;
        var ordered = groups
            .OrderBy(group => group.Role switch { AppRole.Backend => 0, AppRole.Frontend => 1, _ => 2 })
            .ThenBy(group => group.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new WorkspaceViewModel(first.WorkspaceName, first.WorkspacePath, ordered);
    }

    private static bool SamePath(string left, string right) =>
        ConnectionKey.NormalizePath(left) == ConnectionKey.NormalizePath(right);

    private static string WorkspaceName(string folder) =>
        Path.GetFileName(folder.TrimEnd('/', '\\')) is { Length: > 0 } name ? name : folder;
}

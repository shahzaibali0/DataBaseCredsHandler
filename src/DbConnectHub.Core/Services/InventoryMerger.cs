using DbConnectHub.Core.Models;
using DbConnectHub.Core.Util;

namespace DbConnectHub.Core.Services;

/// <summary>What one scan found besides databases, plus where it looked.</summary>
public sealed record InventoryMergeInput(
    InventorySnapshot Existing,
    IReadOnlyList<DiscoveredApp> Apps,
    IReadOnlyCollection<string> ScannedRoots);

/// <summary>Replaces apps under scanned roots with exactly what the scan found and keeps apps elsewhere.</summary>
public static class InventoryMerger
{
    public static InventorySnapshot Merge(InventoryMergeInput input) => new() { Apps = MergeApps(input) };

    // Apps are not user data: under a scanned root they are exactly what this scan found.
    private static List<SavedApp> MergeApps(InventoryMergeInput input) =>
        input.Existing.Apps
            .Where(app => !IsUnderAny(app.AppPath, input.ScannedRoots))
            .Concat(input.Apps
                .GroupBy(app => ConnectionKey.NormalizePath(app.AppPath), StringComparer.Ordinal)
                .Select(group => ToSavedApp(group.First())))
            .ToList();

    private static bool IsUnderAny(string path, IReadOnlyCollection<string> roots) =>
        roots.Any(root => ConnectionKey.IsUnder(path, root));

    private static SavedApp ToSavedApp(DiscoveredApp app) => new()
    {
        WorkspaceName = app.WorkspaceName,
        WorkspacePath = app.WorkspacePath,
        AppName = app.AppName,
        AppPath = app.AppPath,
        Role = app.Role,
        Framework = app.Framework,
        LocalUrl = app.LocalUrl,
        LocalUrlSource = app.LocalUrlSource,
        Stack = app.Stack.ToList(),
        Packages = app.Packages.ToList(),
    };
}

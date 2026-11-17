using DbConnectHub.Core.Models;
using DbConnectHub.Core.Services;

namespace DbConnectHub.Tests.Services;

public sealed class InventoryMergerTests
{
    private const string Root = @"C:\Users\you\Projects";
    private const string AppPath = @"C:\Users\you\Projects\shop\backend";

    [Fact]
    public void Merge_AppsUnderScannedRoot_ReplacesThemWithWhatWasFound()
    {
        var existing = new InventorySnapshot
        {
            Apps =
            [
                new SavedApp { AppPath = AppPath, LocalUrl = "http://localhost:3000" },
                new SavedApp { AppPath = @"D:\Other\web" },
            ],
        };
        DiscoveredApp found = new("shop", @"C:\Users\you\Projects\shop", "backend", AppPath, AppRole.Backend,
            "Next.js", "http://localhost:3001", "package.json dev script");

        var inventory = InventoryMerger.Merge(new InventoryMergeInput(existing, [found], [Root]));

        Assert.Equal(2, inventory.Apps.Count);
        Assert.Equal("http://localhost:3001", inventory.Apps.Single(app => app.AppPath == AppPath).LocalUrl);
        Assert.Contains(inventory.Apps, app => app.AppPath == @"D:\Other\web");
    }

    [Fact]
    public void Merge_AppMissingUnderScannedRoot_RemovesIt()
    {
        var existing = new InventorySnapshot { Apps = [new SavedApp { AppPath = AppPath }] };

        var inventory = InventoryMerger.Merge(new InventoryMergeInput(existing, [], [Root]));

        Assert.Empty(inventory.Apps);
    }
}

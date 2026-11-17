using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Core.Models;
using PgCredsHandler.Storage;
using PgCredsHandler.Tests.Support;

namespace PgCredsHandler.Tests.Storage;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Load_FileMissing_ReturnsDefaults()
    {
        var settings = new JsonSettingsStore(_folder.Combine("settings.json")).Load();

        Assert.Empty(settings.ScanRoots);
        Assert.Contains("node_modules", settings.ExcludedFolderNames);
    }

    [Fact]
    public void Save_ThenLoad_KeepsHiddenFingerprints()
    {
        var path = _folder.Combine("settings.json");
        var settings = new AppSettings { HiddenFingerprints = ["c:/work/shop|localhost|5432|shop|app"] };

        new JsonSettingsStore(path).Save(settings);

        Assert.Equal(settings.HiddenFingerprints, new JsonSettingsStore(path).Load().HiddenFingerprints);
    }

    [Fact]
    public void Load_BrokenFile_ThrowsInsteadOfResettingSettings()
    {
        var path = _folder.WriteFile("settings.json", "[[[");
        var store = new JsonSettingsStore(path);

        Assert.Throws<DataFileUnreadableException>(() => store.Load());
        Assert.Throws<DataFileUnreadableException>(() => store.Save(new AppSettings()));
        Assert.Equal("[[[", File.ReadAllText(path));
    }
}

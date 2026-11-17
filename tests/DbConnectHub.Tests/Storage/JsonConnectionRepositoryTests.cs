using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;
using DbConnectHub.Storage;
using DbConnectHub.Tests.Support;

namespace DbConnectHub.Tests.Storage;

public sealed class JsonConnectionRepositoryTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void GetAll_FileMissing_ReturnsEmpty()
    {
        var repository = new JsonConnectionRepository(_folder.Combine("connections.json"));

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void SaveAll_ThenGetAll_RoundTripsConnections()
    {
        var path = _folder.Combine("connections.json");
        var saved = new SavedConnection { ProjectName = "shop", Host = "localhost", Port = 5433, Database = "shop", Username = "app" };

        new JsonConnectionRepository(path).SaveAll([saved]);
        var loaded = new JsonConnectionRepository(path).GetAll();

        var single = Assert.Single(loaded);
        Assert.Equal(saved.Id, single.Id);
        Assert.Equal(5433, single.Port);
        Assert.Equal("app", single.Username);
    }

    [Fact]
    public void SaveAll_NeverWritesAPasswordField()
    {
        var path = _folder.Combine("connections.json");

        new JsonConnectionRepository(path).SaveAll([new SavedConnection { HasPassword = true }]);
        var json = File.ReadAllText(path);

        Assert.DoesNotContain("\"password\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"hasPassword\": true", json);
    }

    [Fact]
    public void GetAll_BrokenFile_ThrowsAndKeepsABackup()
    {
        var path = _folder.WriteFile("connections.json", "{ this is not json");

        var error = Assert.Throws<DataFileUnreadableException>(() => new JsonConnectionRepository(path).GetAll());

        Assert.Equal(path, error.FilePath);
        Assert.NotNull(error.BackupPath);
        Assert.Equal("{ this is not json", File.ReadAllText(error.BackupPath!));
    }

    [Fact]
    public void SaveAll_AfterBrokenRead_RefusesAndLeavesFileUntouched()
    {
        var path = _folder.WriteFile("connections.json", "{ this is not json");
        var repository = new JsonConnectionRepository(path);
        Assert.Throws<DataFileUnreadableException>(() => repository.GetAll());

        Assert.Throws<DataFileUnreadableException>(() => repository.SaveAll([new SavedConnection()]));

        Assert.Equal("{ this is not json", File.ReadAllText(path));
    }
}

using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Core.Models;
using PgCredsHandler.Scanner;
using PgCredsHandler.Storage;
using PgCredsHandler.Tests.Support;

namespace PgCredsHandler.Tests.Scanner;

public sealed class ScanWorkflowTests : IDisposable
{
    private readonly TempFolder _projects = new();
    private readonly TempFolder _appData = new();
    private readonly InMemoryCredentialStore _credentials = new();

    public void Dispose()
    {
        _projects.Dispose();
        _appData.Dispose();
    }

    [Fact]
    public void ScanAndSave_ProjectWithEnvFile_SavesConnectionAndPasswordSeparately()
    {
        WriteShopProject();
        var stores = CreateStores(_credentials);

        var summary = CreateWorkflow(stores).ScanAndSave(null, CancellationToken.None);

        Assert.Equal(1, summary.Added);
        var saved = Assert.Single(stores.Connections.GetAll());
        Assert.Equal(("shop", "localhost", 5433, "shop_db", "shop_user"),
            (saved.ProjectName, saved.Host, saved.Port, saved.Database, saved.Username));
        Assert.True(saved.HasPassword);
        Assert.Equal("s3cret!", _credentials.Read(saved.Id));
        Assert.DoesNotContain("s3cret!", File.ReadAllText(_appData.Combine("connections.json")));
    }

    [Fact]
    public void ScanAndSave_RunTwice_AddsNothingTheSecondTime()
    {
        WriteShopProject();
        var workflow = CreateWorkflow(CreateStores(_credentials));

        workflow.ScanAndSave(null, CancellationToken.None);
        var second = workflow.ScanAndSave(null, CancellationToken.None);

        Assert.Equal(0, second.Added);
        Assert.Equal(1, second.Total);
    }

    [Fact]
    public void ScanAndSave_NoChosenFolders_ScansTheDefaultRoots()
    {
        WriteShopProject();
        var stores = CreateStores(_credentials);
        var workflow = new ScanWorkflow(stores, () => [_projects.Path]);

        var summary = workflow.ScanAndSave(null, CancellationToken.None);

        Assert.Equal([_projects.Path], summary.ScannedRoots);
        Assert.Equal(1, summary.Found);
    }

    [Fact]
    public void ScanAndSave_ChosenFolders_IgnoresTheDefaultRoots()
    {
        WriteShopProject();
        var stores = CreateStores(_credentials);
        stores.Settings.Save(new AppSettings { ScanRoots = [_projects.Path] });
        var workflow = new ScanWorkflow(stores, () => throw new InvalidOperationException("drives must not be listed"));

        var summary = workflow.ScanAndSave(null, CancellationToken.None);

        Assert.Equal(1, summary.Found);
    }

    [Fact]
    public void ScanAndSave_Cancelled_SavesNothing()
    {
        WriteShopProject();
        var stores = CreateStores(_credentials);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => CreateWorkflow(stores).ScanAndSave(null, cancellation.Token));

        Assert.False(File.Exists(_appData.Combine("connections.json")));
    }

    [Fact]
    public void ScanAndSave_PasswordCannotBeStored_SavesNoList()
    {
        WriteShopProject();
        var stores = CreateStores(new RefusingCredentialStore());

        Assert.Throws<InvalidOperationException>(() => CreateWorkflow(stores).ScanAndSave(null, CancellationToken.None));

        Assert.False(File.Exists(_appData.Combine("connections.json")));
    }

    [Fact]
    public void ScanAndSave_ProjectRemoved_KeepsEntryAsStale()
    {
        var envFile = WriteShopProject();
        var stores = CreateStores(_credentials);
        var workflow = CreateWorkflow(stores);
        workflow.ScanAndSave(null, CancellationToken.None);

        File.Delete(envFile);
        var second = workflow.ScanAndSave(null, CancellationToken.None);

        Assert.Equal(1, second.MarkedStale);
        Assert.True(Assert.Single(stores.Connections.GetAll()).IsStale);
    }

    [Fact]
    public void ScanAndSave_ReportsProgressForEachConfigFile()
    {
        WriteShopProject();
        var reports = new List<ScanProgress>();

        CreateWorkflow(CreateStores(_credentials)).ScanAndSave(new SynchronousProgress(reports), CancellationToken.None);

        Assert.Single(reports);
        Assert.EndsWith(".env", reports[0].CurrentFile);
    }

    private string WriteShopProject()
    {
        _projects.WriteFile("shop/package.json", "{}");
        return _projects.WriteFile("shop/.env",
            "DB_HOST=localhost\nDB_PORT=5433\nDB_NAME=shop_db\nDB_USER=shop_user\nDB_PASSWORD=s3cret!\n");
    }

    private DataStores CreateStores(ICredentialStore credentials) => new(
        new JsonConnectionRepository(_appData.Combine("connections.json")),
        credentials,
        new JsonSettingsStore(_appData.Combine("settings.json")));

    private ScanWorkflow CreateWorkflow(DataStores stores) => new(stores, () => [_projects.Path]);

    // Progress<T> posts to a thread pool; this one records on the calling thread so the test can assert.
    private sealed class SynchronousProgress(List<ScanProgress> reports) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => reports.Add(value);
    }
}

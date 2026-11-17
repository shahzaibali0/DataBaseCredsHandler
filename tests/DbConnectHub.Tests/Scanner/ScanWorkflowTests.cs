using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;
using DbConnectHub.Scanner;
using DbConnectHub.Storage;
using DbConnectHub.Tests.Support;

namespace DbConnectHub.Tests.Scanner;

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
    public void ScanAndSave_ProjectWithEnvAndExampleFiles_SavesOnlyTheRealDatabase()
    {
        _projects.WriteFile("shop/package.json", "{}");
        _projects.WriteFile("shop/.env", "DATABASE_URL=postgresql://postgres:s3cret!@localhost:5432/shop\n");
        _projects.WriteFile("shop/.env.example", "DATABASE_URL=postgresql://user:password@host:port/dbname\n");
        _projects.WriteFile("shop/.env.production.example", "DATABASE_URL=postgresql://USER:PASSWORD@HOST:PORT/DATABASE\n");
        var stores = CreateStores(_credentials);

        var summary = CreateWorkflow(stores).ScanAndSave(null, CancellationToken.None);

        Assert.Equal(1, summary.Found);
        Assert.Equal("shop", Assert.Single(stores.Connections.GetAll()).Database);
    }

    [Fact]
    public void ScanAndSave_OnlyAPlaceholderExample_SavesNothing()
    {
        _projects.WriteFile("api/package.json", "{}");
        _projects.WriteFile("api/.env.example", "DATABASE_URL=postgresql://user:password@host:port/dbname\n");
        var stores = CreateStores(_credentials);

        var summary = CreateWorkflow(stores).ScanAndSave(null, CancellationToken.None);

        Assert.Equal(0, summary.Found);
        Assert.Empty(stores.Connections.GetAll());
    }

    [Fact]
    public void ScanAndSave_RepoWithBackendAndFrontend_FindsAppsPortsAndDatabase()
    {
        _projects.WriteFile("AcmeRepo/.git/HEAD", "ref: refs/heads/main\n");
        _projects.WriteFile("AcmeRepo/acme_backend/package.json",
            """{ "scripts": { "dev": "next dev --port 3001" }, "dependencies": { "next": "16" } }""");
        _projects.WriteFile("AcmeRepo/acme_backend/.env",
            "DATABASE_URL=postgresql://postgres:db-pass-123@localhost:5432/acme\n");
        _projects.WriteFile("AcmeRepo/acme_frontend/package.json",
            """{ "scripts": { "dev": "next dev" }, "dependencies": { "next": "16" } }""");
        var stores = CreateStores(_credentials);

        var summary = CreateWorkflow(stores).ScanAndSave(null, CancellationToken.None);

        var inventory = stores.Inventory.Load();
        Assert.Equal((1, 2), (summary.Found, summary.AppsFound));
        Assert.All(inventory.Apps, app => Assert.Equal("AcmeRepo", app.WorkspaceName));
        Assert.Equal(
            [("acme_backend", AppRole.Backend, "http://localhost:3001"), ("acme_frontend", AppRole.Frontend, "http://localhost:3000")],
            inventory.Apps.OrderBy(app => app.AppName).Select(app => (app.AppName, app.Role, app.LocalUrl)));
        var connection = Assert.Single(stores.Connections.GetAll());
        Assert.Equal("acme", connection.Database);
        Assert.Equal("db-pass-123", _credentials.Read(connection.Id));
    }

    [Fact]
    public void ScanAndSave_EnvironmentSecrets_AppearNowhere()
    {
        const string jwtValue = "jwt-guard-value-123456";
        const string nextAuthValue = "nextauth-guard-value-123456";
        _projects.WriteFile("GuardRepo/.git/HEAD", "ref: refs/heads/main\n");
        _projects.WriteFile("GuardRepo/backend/package.json", "{}");
        _projects.WriteFile("GuardRepo/backend/.env",
            $"DATABASE_URL=postgresql://postgres:db-password@localhost:5432/guard\nJWT_SECRET={jwtValue}\nNEXTAUTH_SECRET={nextAuthValue}\n");
        var stores = CreateStores(_credentials);

        CreateWorkflow(stores).ScanAndSave(null, CancellationToken.None);

        Assert.False(_credentials.ContainsValue(jwtValue));
        Assert.False(_credentials.ContainsValue(nextAuthValue));
        AssertFileDoesNotContain(_appData.Combine("inventory.json"), jwtValue, nextAuthValue);
        AssertFileDoesNotContain(_appData.Combine("connections.json"), jwtValue, nextAuthValue);
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

        Assert.Equal([".env", "package.json"], reports.Select(report => Path.GetFileName(report.CurrentFile)).Order());
        Assert.Equal([1, 2], reports.Select(report => report.FilesScanned).Order());
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
        new JsonSettingsStore(_appData.Combine("settings.json")),
        new JsonInventoryRepository(_appData.Combine("inventory.json")));

    private ScanWorkflow CreateWorkflow(DataStores stores) => new(stores, () => [_projects.Path]);

    private static void AssertFileDoesNotContain(string path, params string[] forbiddenValues)
    {
        var json = File.ReadAllText(path);
        Assert.All(forbiddenValues, forbiddenValue => Assert.DoesNotContain(forbiddenValue, json, StringComparison.Ordinal));
    }

    // Progress<T> posts to a thread pool; this one records on the calling thread so the test can assert.
    private sealed class SynchronousProgress(List<ScanProgress> reports) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => reports.Add(value);
    }
}

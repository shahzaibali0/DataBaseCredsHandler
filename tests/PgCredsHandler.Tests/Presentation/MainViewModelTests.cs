using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Presentation;
using PgCredsHandler.Scanner;
using PgCredsHandler.Storage;
using PgCredsHandler.Tests.Support;

namespace PgCredsHandler.Tests.Presentation;

public sealed class MainViewModelTests : IDisposable
{
    private readonly TempFolder _projects = new();
    private readonly TempFolder _appData = new();
    private readonly FakeDesktopShell _shell = new();
    private readonly FakeStartup _startup = new();
    private readonly FakeDatabaseTools _tools = new();
    private readonly FakeClipboard _clipboard = new();

    public void Dispose()
    {
        _projects.Dispose();
        _appData.Dispose();
    }

    [Fact]
    public void Load_NothingSavedYet_ShowsTheWelcome()
    {
        var viewModel = CreateViewModel();

        viewModel.Load();

        Assert.True(viewModel.ShowWelcome);
        Assert.StartsWith("Scans all local drives", viewModel.ScanScope);
    }

    [Fact]
    public async Task Scan_ShowsTheProjectWithPortsStackAndDatabase()
    {
        WriteRepository();
        var viewModel = await ScannedViewModel();

        var project = Assert.Single(viewModel.Workspaces);
        Assert.Equal(("ShopRepo", "SR"), (project.Name, project.Initials));
        Assert.Equal([("Backend", ":3001"), ("Frontend", ":3000")], project.Servers.Select(app => (app.RoleLabel, app.PortLabel)));
        Assert.Equal("Next.js 16 · React 19 · TypeScript", project.StackSummary);
        Assert.Equal([new PackageGroupViewModel("Database", "Prisma")], project.Packages);
        var database = Assert.Single(project.Databases);
        Assert.Equal(("shop_db", "localhost", 5432, "shop_user"), (database.Database, database.Host, database.Port, database.Username));
        Assert.Equal((1, 2, 1), (viewModel.ProjectCount, viewModel.AppCount, viewModel.DatabaseCount));
    }

    [Fact]
    public async Task Password_IsMaskedUntilTheEyeIsClickedAndMaskedAgainOnClose()
    {
        WriteRepository();
        var viewModel = await ScannedViewModel();
        var project = Assert.Single(viewModel.Workspaces);
        var database = Assert.Single(project.Databases);
        viewModel.OpenProjectCommand.Execute(project);
        Assert.Equal(ConnectionRowViewModel.Mask, database.DisplayPassword);

        database.ToggleRevealCommand.Execute(null);
        Assert.Equal("secret-db-1", database.DisplayPassword);

        viewModel.CloseProjectCommand.Execute(null);
        Assert.Equal((ConnectionRowViewModel.Mask, false), (database.DisplayPassword, viewModel.IsProjectOpen));
    }

    [Fact]
    public async Task Rescan_MasksAShownPasswordBeforeReplacingRows()
    {
        WriteRepository();
        var viewModel = await ScannedViewModel();
        var oldDatabase = Assert.Single(Assert.Single(viewModel.Workspaces).Databases);
        oldDatabase.ToggleRevealCommand.Execute(null);

        await viewModel.ScanCommand.ExecuteAsync(null);

        Assert.Equal((ConnectionRowViewModel.Mask, false), (oldDatabase.DisplayPassword, oldDatabase.IsRevealed));
    }

    [Fact]
    public async Task CopyPassword_PutsItOnTheClipboardAsASecret()
    {
        WriteRepository();
        var database = Assert.Single(Assert.Single((await ScannedViewModel()).Workspaces).Databases);

        database.CopyPasswordCommand.Execute(null);

        Assert.Equal(("secret-db-1", true), (_clipboard.Text, _clipboard.WasSecret));
    }

    [Fact]
    public async Task ToolButtons_AreEnabledOnlyForInstalledTools_AndOpenWithTheFullConnection()
    {
        _tools.Installed.Add(DatabaseTool.DBeaver);
        WriteRepository();
        var database = Assert.Single(Assert.Single((await ScannedViewModel()).Workspaces).Databases);

        Assert.Equal((true, false, false), (database.OpenInDbeaverCommand.CanExecute(null),
            database.OpenInPgAdminCommand.CanExecute(null), database.OpenInDbVisualizerCommand.CanExecute(null)));
        database.OpenInDbeaverCommand.Execute(null);

        var (tool, request) = Assert.Single(_tools.Opened);
        Assert.Equal(DatabaseTool.DBeaver, tool);
        Assert.Equal(new DatabaseLaunchRequest("shop_backend · shop_db", "localhost", 5432, "shop_db", "shop_user", "secret-db-1"), request);
    }

    [Fact]
    public async Task Search_NoMatch_SaysSoInsteadOfShowingAnEmptyList()
    {
        WriteRepository();
        var viewModel = await ScannedViewModel();

        viewModel.SearchText = "nothing-like-this";

        Assert.Empty(viewModel.Workspaces);
        Assert.True(viewModel.HasNoMatches);
    }

    [Fact]
    public async Task OpenLocalUrl_OpensTheAppsDevServer()
    {
        WriteRepository();
        var viewModel = await ScannedViewModel();

        viewModel.OpenLocalUrlCommand.Execute(viewModel.Workspaces[0].Apps[0].LocalUrl);

        Assert.Equal(["http://localhost:3001"], _shell.OpenedUrls);
    }

    [Fact]
    public async Task ChooseFolder_SavesItAndScansOnlyThatFolder()
    {
        WriteRepository();
        using var elsewhere = new TempFolder();
        elsewhere.WriteFile("other/.env", "DATABASE_URL=postgresql://app:secret@localhost:5432/other_db\n");
        _shell.FolderToPick = _projects.Combine("ShopRepo");
        var viewModel = CreateViewModel(defaultRoots: () => [elsewhere.Path]);
        viewModel.Load();

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal([_shell.FolderToPick], viewModel.ChosenFolders);
        Assert.Equal("ShopRepo", Assert.Single(viewModel.Workspaces).Name);
    }

    [Fact]
    public void Load_StartWithWindowsOnByDefault_RegistersTheStartupEntry()
    {
        var viewModel = CreateViewModel();

        viewModel.Load();

        Assert.True(viewModel.StartWithWindows);
        Assert.True(_startup.IsEnabled);
    }

    [Fact]
    public void Load_UnreadableDataFile_ShowsTheErrorAndBlocksScanning()
    {
        _appData.WriteFile("connections.json", "{ broken");
        var viewModel = CreateViewModel();

        viewModel.Load();

        Assert.True(viewModel.HasError);
        Assert.Contains("could not be read", viewModel.ErrorMessage);
        Assert.False(viewModel.ScanCommand.CanExecute(null));
    }

    private async Task<MainViewModel> ScannedViewModel()
    {
        var viewModel = CreateViewModel();
        viewModel.Load();
        await viewModel.ScanCommand.ExecuteAsync(null);
        return viewModel;
    }

    private void WriteRepository()
    {
        _projects.WriteFile("ShopRepo/.git/HEAD", "ref: refs/heads/main\n");
        _projects.WriteFile("ShopRepo/shop_backend/package.json",
            """{ "scripts": { "dev": "next dev --port 3001" }, "dependencies": { "next": "16", "react": "19", "@prisma/client": "6" }, "devDependencies": { "typescript": "5" } }""");
        _projects.WriteFile("ShopRepo/shop_backend/.env",
            "DATABASE_URL=postgresql://shop_user:secret-db-1@localhost:5432/shop_db\n");
        _projects.WriteFile("ShopRepo/shop_frontend/package.json",
            """{ "scripts": { "dev": "next dev" }, "dependencies": { "next": "16" } }""");
    }

    private MainViewModel CreateViewModel(Func<IReadOnlyList<string>>? defaultRoots = null)
    {
        var stores = new DataStores(
            new JsonConnectionRepository(_appData.Combine("connections.json")),
            new InMemoryCredentialStore(),
            new JsonSettingsStore(_appData.Combine("settings.json")),
            new JsonInventoryRepository(_appData.Combine("inventory.json")));
        var workflow = new ScanWorkflow(stores, defaultRoots ?? (() => [_projects.Path]));
        var databases = new DatabaseActions(stores.Credentials, _tools, _clipboard);
        return new MainViewModel(new AppServices(workflow, stores, _shell, _startup, databases));
    }

    private sealed class FakeDatabaseTools : IDatabaseTools
    {
        public HashSet<DatabaseTool> Installed { get; } = [];

        public List<(DatabaseTool Tool, DatabaseLaunchRequest Request)> Opened { get; } = [];

        public bool IsInstalled(DatabaseTool tool) => Installed.Contains(tool);

        public ToolLaunch Open(DatabaseTool tool, DatabaseLaunchRequest request)
        {
            Opened.Add((tool, request));
            return new ToolLaunch(true, "opened");
        }
    }

    private sealed class FakeClipboard : IClipboard
    {
        public string? Text { get; private set; }

        public bool WasSecret { get; private set; }

        public void CopyText(string text, bool isSecret) => (Text, WasSecret) = (text, isSecret);
    }
}

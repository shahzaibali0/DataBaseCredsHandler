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
        Assert.False(viewModel.HasConnections);
        Assert.StartsWith("Scans all local drives", viewModel.ScanScope);
    }

    [Fact]
    public async Task Scan_GroupsDatabasesByProjectAndCountsThem()
    {
        WriteProjects();
        var viewModel = CreateViewModel();
        viewModel.Load();

        await viewModel.ScanCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowWelcome);
        Assert.Equal(["billing", "shop"], viewModel.Groups.Select(group => group.ProjectName));
        Assert.Equal((2, 3, 1), (viewModel.ProjectCount, viewModel.DatabaseCount, viewModel.RemoteCount));
        Assert.StartsWith("Last scan found 3 databases: 3 new", viewModel.Summary);
    }

    [Fact]
    public async Task SearchText_KeepsOnlyMatchingDatabases()
    {
        WriteProjects();
        var viewModel = await ScannedViewModel();

        viewModel.SearchText = "ledger";

        var group = Assert.Single(viewModel.Groups);
        Assert.Equal("ledger", Assert.Single(group.Connections).Database);
    }

    [Fact]
    public async Task HostFilter_OnANetwork_ShowsOnlyRemoteAndPrivateHosts()
    {
        WriteProjects();
        var viewModel = await ScannedViewModel();

        viewModel.HostFilterIndex = MainViewModel.ShowOnNetwork;

        var row = Assert.Single(Assert.Single(viewModel.Groups).Connections);
        Assert.Equal("billing-prod.example.com", row.Host);
    }

    [Fact]
    public async Task SearchWithNoMatch_SaysSoInsteadOfShowingAnEmptyList()
    {
        WriteProjects();
        var viewModel = await ScannedViewModel();

        viewModel.SearchText = "nothing-like-this";

        Assert.Empty(viewModel.Groups);
        Assert.True(viewModel.HasNoMatches);
    }

    [Fact]
    public async Task ChooseFolder_SavesItAndScansOnlyThatFolder()
    {
        WriteProjects();
        using var elsewhere = new TempFolder();
        elsewhere.WriteFile("other/.env", "DATABASE_URL=postgresql://app:secret@localhost:5432/other_db\n");
        _shell.FolderToPick = _projects.Combine("shop");
        var viewModel = CreateViewModel(defaultRoots: () => [elsewhere.Path]);
        viewModel.Load();

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal([_shell.FolderToPick], viewModel.ChosenFolders);
        Assert.Equal("shop", Assert.Single(viewModel.Groups).ProjectName);
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

    private void WriteProjects()
    {
        _projects.WriteFile("shop/package.json", "{}");
        _projects.WriteFile("shop/.env", "DATABASE_URL=postgresql://shop_user:secret@localhost:5432/shop_db\n");
        _projects.WriteFile("billing/package.json", "{}");
        _projects.WriteFile("billing/.env",
            "DATABASE_URL=postgresql://app:secret@localhost:5432/invoices\n"
            + "LEDGER_DATABASE_URL=postgresql://app:secret@billing-prod.example.com:5432/ledger\n");
    }

    private MainViewModel CreateViewModel(Func<IReadOnlyList<string>>? defaultRoots = null)
    {
        var stores = new DataStores(
            new JsonConnectionRepository(_appData.Combine("connections.json")),
            new InMemoryCredentialStore(),
            new JsonSettingsStore(_appData.Combine("settings.json")));
        var workflow = new ScanWorkflow(stores, defaultRoots ?? (() => [_projects.Path]));
        return new MainViewModel(workflow, stores, _shell);
    }

    private sealed class FakeDesktopShell : IDesktopShell
    {
        public string? FolderToPick { get; set; }

        public string DataFolderPath => @"C:\Users\you\AppData\Roaming\PgCredsHandler";

        public void OpenFolder(string folderPath) { }

        public Task<string?> PickFolderAsync() => Task.FromResult(FolderToPick);
    }
}

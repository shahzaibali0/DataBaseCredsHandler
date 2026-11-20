using DbConnectHub.Integrations;
using DbConnectHub.Tests.Support;

namespace DbConnectHub.Tests.Integrations;

public sealed class DbToolLauncherTests : IDisposable
{
    private static readonly DbConnectionInfo Shop = new("ShopRepo · shop", "localhost", 5433, "shop", "postgres", "p@ss=w0rd");

    private const string AppPath = "C:\\Apps\\DB Connect Hub\\DbConnectHub.exe";

    // "localhost\n5433\npostgres\nshop" in hex: the only part of the command that comes from a config file.
    private const string ShopPasswordCommand =
        "\"C:\\Apps\\DB Connect Hub\\DbConnectHub.exe\" --pgadmin-password 6c6f63616c686f73740a353433330a706f7374677265730a73686f70";

    private readonly TempFolder _programs = new();
    private readonly FakeProcessRunner _processes = new();

    public void Dispose() => _programs.Dispose();

    [Fact]
    public void DbeaverArguments_DescribeTheWholeConnection()
    {
        var arguments = DbToolLauncher.DbeaverArguments(Shop)!;

        Assert.Equal("-con", arguments[0]);
        Assert.Equal(
            "driver=postgresql|host=localhost|port=5433|database=shop|user=postgres|password=p@ss=w0rd|name=ShopRepo · shop|connect=true",
            arguments[1]);
    }

    [Fact]
    public void DbeaverArguments_ValueWithAPipe_IsRefusedRatherThanCorrupted()
    {
        Assert.Null(DbToolLauncher.DbeaverArguments(Shop with { Password = "a|b" }));
    }

    [Fact]
    public void PgAdminServersJson_NeverContainsThePassword()
    {
        var json = PgAdminServers.ServersJson(Shop, AppPath);

        Assert.Contains("\"Host\":\"localhost\"", json);
        Assert.Contains("\"Port\":5433", json);
        Assert.DoesNotContain("p@ss", json);
        var fields = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("Servers").GetProperty("1")
            .EnumerateObject().Select(field => field.Name);
        Assert.Empty(fields.Intersect(["Password", "PassFile", "SavePassword"]));
    }

    [Fact]
    public async Task Open_DBeaverInstalled_StartsItWithTheConnection()
    {
        var dbeaver = _programs.WriteFile("DBeaver/dbeaver.exe", "");

        var result = await CreateLauncher().OpenAsync(DbTool.DBeaver, Shop, CancellationToken.None);

        Assert.True(result.Success);
        var started = Assert.Single(_processes.Started);
        Assert.Equal(dbeaver, started.Executable);
        Assert.Equal("-con", started.Arguments[0]);
    }

    [Fact]
    public async Task Open_ToolNotInstalled_SaysSoAndStartsNothing()
    {
        var result = await CreateLauncher().OpenAsync(DbTool.DbVisualizer, Shop, CancellationToken.None);

        Assert.Equal((false, "DbVisualizer isn't installed on this PC."), (result.Success, result.Message));
        Assert.Empty(_processes.Started);
    }

    [Fact]
    public async Task Open_PgAdminDatabaseMissing_AddsAServerNamedForTheProjectShowingOnlyThatDatabase()
    {
        var pgAdmin = InstallPgAdmin();
        _processes.ExistingServers = """{ "Servers": { "1": { "Name": "PostgreSQL 18", "Group": "Servers", "Host": "localhost", "Port": 5433, "Username": "postgres" } } }""";

        var result = await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal(["dump-servers", "load-servers"], _processes.Ran.Select(run => run.Arguments[1]));
        Assert.Equal("ShopRepo · shop", ImportedServer().GetProperty("Name").GetString());
        Assert.Equal(("shop", "databases"),
            (ImportedServer().GetProperty("DBRestriction").GetString(), ImportedServer().GetProperty("DBRestrictionType").GetString()));
        Assert.Contains("\"Group\":\"DB Connect Hub\"", _processes.ImportedJson);
        Assert.Contains("\"MaintenanceDB\":\"shop\"", _processes.ImportedJson);
        Assert.All(_processes.Ran, run => Assert.False(File.Exists(run.Arguments[2])));
        Assert.Equal(pgAdmin, Assert.Single(_processes.Started).Executable);
        Assert.True(result.Success);
        Assert.Contains("added", result.Message);
    }

    [Fact]
    public async Task Open_PgAdminLoginMissing_TellsPgAdminToAskThisAppForThePassword()
    {
        InstallPgAdmin();

        await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal(ShopPasswordCommand, ImportedServer().GetProperty("PasswordExecCommand").GetString());
    }

    [Theory]
    [InlineData("C:\\Apps\\100%\\DbConnectHub.exe")]
    [InlineData("C:\\Apps\\\"odd\"\\DbConnectHub.exe")]
    [InlineData("C:\\Tools!x!\\DbConnectHub.exe")] // cmd with delayed expansion turns !x! into nothing, even in quotes
    [InlineData("C:\\Tools^1\\DbConnectHub.exe")]
    public async Task Open_AppPathCmdCannotQuoteSafely_LeavesThePasswordToPgAdmin(string appPath)
    {
        InstallPgAdmin();

        await CreateLauncher(appPath).OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.False(ImportedServer().TryGetProperty("PasswordExecCommand", out _));
    }

    [Theory]
    [InlineData("s3cret-P@ss!&|^%", true)]
    [InlineData(" padded ", false)]  // pgAdmin strips the output, so the spaces would be lost
    [InlineData("pässwörd", false)]  // pgAdmin's Python may read the output as cp1252 or as UTF-8
    [InlineData("tab\there", false)]
    [InlineData("", false)]
    public void CanSendPassword_OnlyPrintableAsciiWithoutOuterSpaces(string password, bool expected)
    {
        Assert.Equal(expected, PgAdminServers.CanSendPassword(password));
    }

    [Fact]
    public async Task Open_PgAdminDatabaseAlreadyInTheAppGroup_AddsNothing()
    {
        InstallPgAdmin();
        _processes.ExistingServers = ExistingAppServer(ShopPasswordCommand);

        var result = await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal("dump-servers", Assert.Single(_processes.Ran).Arguments[1]);
        Assert.Single(_processes.Started);
        Assert.True(result.Success);
        Assert.Contains("already", result.Message);
    }

    [Fact]
    public async Task Open_PgAdminTwiceWhileItsDatabaseIsUnchanged_ListsItsServersOnlyOnce()
    {
        InstallPgAdmin();
        File.WriteAllText(DesktopDatabasePath, "servers");
        _processes.ExistingServers = ExistingAppServer(ShopPasswordCommand);
        var launcher = CreateLauncher();

        await launcher.OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);
        var second = await launcher.OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal("dump-servers", Assert.Single(_processes.Ran).Arguments[1]);
        Assert.Contains("already", second.Message);
    }

    [Fact]
    public async Task Open_PgAdminAfterItsDatabaseChanged_ListsItsServersAgain()
    {
        InstallPgAdmin();
        File.WriteAllText(DesktopDatabasePath, "servers");
        _processes.ExistingServers = ExistingAppServer(ShopPasswordCommand);
        var launcher = CreateLauncher();
        await launcher.OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        // The user deleted the entry in pgAdmin, which rewrites its database.
        _processes.ExistingServers = """{ "Servers": {} }""";
        File.WriteAllText(DesktopDatabasePath, "servers, one fewer");
        var second = await launcher.OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal(["dump-servers", "dump-servers", "load-servers"], _processes.Ran.Select(run => run.Arguments[1]));
        Assert.Contains("added", second.Message);
    }

    [Fact]
    public async Task Open_PgAdminAfterAddingAServer_ListsItsServersAgainRatherThanAddingTwice()
    {
        InstallPgAdmin();
        File.WriteAllText(DesktopDatabasePath, "servers");
        var launcher = CreateLauncher();
        await launcher.OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        _processes.ExistingServers = ExistingAppServer(ShopPasswordCommand);
        var second = await launcher.OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal(["dump-servers", "load-servers", "dump-servers"], _processes.Ran.Select(run => run.Arguments[1]));
        Assert.Contains("already", second.Message);
    }

    [Fact]
    public async Task Open_PgAdminTwiceAtTheSameTime_AddsTheServerOnlyOnce()
    {
        InstallPgAdmin();
        File.WriteAllText(DesktopDatabasePath, "servers");
        var dumpStarted = new TaskCompletionSource();
        _processes.DumpStarted = dumpStarted.Task;
        var launcher = CreateLauncher();

        var first = launcher.OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);
        var second = launcher.OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);
        dumpStarted.SetResult();
        var messages = (await Task.WhenAll(first, second)).Select(result => result.Message).ToList();

        Assert.Single(_processes.Ran, run => run.Arguments[1] == "load-servers");
        Assert.Single(messages, message => message.Contains("already"));
    }

    [Fact]
    public async Task Open_PgAdminEntryWithAnOldName_AsksToDeleteIt()
    {
        InstallPgAdmin();
        _processes.ExistingServers = ExistingAppServer(ShopPasswordCommand).Replace("ShopRepo", "backend");

        var result = await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal("dump-servers", Assert.Single(_processes.Ran).Arguments[1]);
        Assert.Contains("Delete", result.Message);
    }

    [Fact]
    public async Task Open_OtherDatabaseOnTheSameLogin_GetsItsOwnEntry()
    {
        InstallPgAdmin();
        _processes.ExistingServers = ExistingAppServer(ShopPasswordCommand);

        await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop with { Name = "BlogRepo · blog", Database = "blog" }, CancellationToken.None);

        Assert.Equal(["dump-servers", "load-servers"], _processes.Ran.Select(run => run.Arguments[1]));
        Assert.Equal("BlogRepo · blog", ImportedServer().GetProperty("Name").GetString());
    }

    // pgAdmin's setup.py can only add servers, so an entry from an older version (or from before the app moved)
    // can't be fixed in place; adding another would leave two with the same name.
    [Fact]
    public async Task Open_PgAdminEntryWithoutTheCurrentPasswordCommand_AsksToDeleteItInsteadOfAddingTwice()
    {
        InstallPgAdmin();
        _processes.ExistingServers = ExistingAppServer(passwordCommand: null);

        var result = await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal("dump-servers", Assert.Single(_processes.Ran).Arguments[1]);
        Assert.Single(_processes.Started);
        Assert.Contains("Delete", result.Message);
        Assert.Contains("older entry for shop", result.Message);
    }

    [Fact]
    public async Task Open_PgAdminImportAddsNoServer_SaysItCouldNotAddIt()
    {
        InstallPgAdmin();
        _processes.ImportOutput = "Error: user not found";

        var result = await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.Equal(2, _processes.Ran.Count);
        Assert.Single(_processes.Started);
        Assert.Contains("couldn't be added", result.Message);
    }

    [Fact]
    public async Task Open_PgAdmin_RunsItsScriptWithItsOwnRuntimeFolderOnThePath()
    {
        InstallPgAdmin();

        await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        // pgAdmin's Python finds libpq.dll only through the PATH; without its runtime folder every import fails.
        var runtimeFolder = Path.GetFullPath(_programs.Combine("pgAdmin 4/runtime"));
        Assert.All(_processes.Ran, run => Assert.Equal(runtimeFolder, run.ExtraSearchFolder));
    }

    [Fact]
    public async Task Open_PgAdminAlreadyOpen_SaysToRefreshItsServerList()
    {
        InstallPgAdmin();
        _processes.StartOutcome = StartOutcome.HandedOff;

        var result = await CreateLauncher().OpenAsync(DbTool.PgAdmin, Shop, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("already open", result.Message);
        Assert.Contains("Refresh", result.Message);
    }

    [Fact]
    public async Task Open_WindowNotUpInTime_SaysItIsStillStarting()
    {
        _programs.WriteFile("DBeaver/dbeaver.exe", "");
        _processes.StartOutcome = StartOutcome.StillStarting;

        var result = await CreateLauncher().OpenAsync(DbTool.DBeaver, Shop, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("still starting", result.Message);
    }

    [Fact]
    public void Find_PgAdminBundledWithPostgreSql_PrefersTheNewestVersion()
    {
        _programs.WriteFile("PostgreSQL/9/pgAdmin 4/runtime/pgAdmin4.exe", "");
        var newest = _programs.WriteFile("PostgreSQL/18/pgAdmin 4/runtime/pgAdmin4.exe", "");

        Assert.Equal(newest, new DbToolLocator([_programs.Path]).Find(DbTool.PgAdmin));
    }

    [Fact]
    public void IsInstalled_FindsOnlyWhatIsThere()
    {
        _programs.WriteFile("DBeaver/dbeaver.exe", "");
        var launcher = CreateLauncher();

        Assert.Equal((true, false, false),
            (launcher.IsInstalled(DbTool.DBeaver), launcher.IsInstalled(DbTool.PgAdmin), launcher.IsInstalled(DbTool.DbVisualizer)));
    }

    private string InstallPgAdmin()
    {
        _programs.WriteFile("pgAdmin 4/python/python.exe", "");
        _programs.WriteFile("pgAdmin 4/web/setup.py", "");
        return _programs.WriteFile("pgAdmin 4/runtime/pgAdmin4.exe", "");
    }

    private DbToolLauncher CreateLauncher(string appPath = AppPath) => new(
        new DbToolLocator([_programs.Path]), _processes, new PgAdminServers(_processes, appPath, DesktopDatabasePath));

    // A made-up pgadmin4.db in the temp folder; it doesn't exist until a test writes it.
    private string DesktopDatabasePath => System.IO.Path.Combine(_programs.Path, "pgadmin4.db");

    private System.Text.Json.JsonElement ImportedServer() =>
        System.Text.Json.JsonDocument.Parse(_processes.ImportedJson).RootElement.GetProperty("Servers").GetProperty("1");

    private static string ExistingAppServer(string? passwordCommand) => System.Text.Json.JsonSerializer.Serialize(new
    {
        Servers = new Dictionary<string, object?>
        {
            ["1"] = new Dictionary<string, object?>
            {
                ["Name"] = "ShopRepo · shop", ["Group"] = "DB Connect Hub", ["Host"] = "LOCALHOST", ["Port"] = 5433,
                ["Username"] = "postgres", ["DBRestriction"] = "shop", ["PasswordExecCommand"] = passwordCommand,
            },
        },
    });

    /// <summary>Plays pgAdmin's setup.py: dump-servers writes the server list, load-servers prints what it added.</summary>
    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string ExistingServers { get; set; } = """{ "Servers": {} }""";

        public string ImportOutput { get; set; } = "Added 1 Server Group(s) and 1 Server(s).";

        public string ImportedJson { get; private set; } = "";

        public StartOutcome StartOutcome { get; set; } = StartOutcome.WindowShown;

        public List<(string Executable, IReadOnlyList<string> Arguments)> Started { get; } = [];

        public List<(IReadOnlyList<string> Arguments, string? ExtraSearchFolder)> Ran { get; } = [];

        public Task<StartOutcome> StartAsync(ProcessCommand command, TimeSpan windowTimeout, CancellationToken cancellationToken)
        {
            Started.Add((command.ExecutablePath, command.Arguments));
            return Task.FromResult(StartOutcome);
        }

        /// <summary>When set, dump-servers waits for it, so a test can start two opens side by side.</summary>
        public Task? DumpStarted { get; set; }

        public async Task<ProcessRun> RunAsync(ProcessCommand command, TimeSpan timeout, CancellationToken cancellationToken)
        {
            lock (Ran) Ran.Add((command.Arguments, command.ExtraSearchFolder)); // two opens may run side by side
            var file = command.Arguments[2];
            if (command.Arguments[1] == "dump-servers")
            {
                var listedServers = ExistingServers; // pgAdmin reads its list when the dump starts
                if (DumpStarted is not null) await DumpStarted;
                File.WriteAllText(file, listedServers);
                return new ProcessRun(0, "Configuration for 1 servers dumped");
            }

            ImportedJson = File.ReadAllText(file); // the import file exists while pgAdmin reads it
            ExistingServers = ImportedJson; // like pgAdmin, the next dump lists what was added
            return new ProcessRun(0, ImportOutput);
        }
    }
}

using PgCredsHandler.Integrations;
using PgCredsHandler.Tests.Support;

namespace PgCredsHandler.Tests.Integrations;

public sealed class DbToolLauncherTests : IDisposable
{
    private static readonly DbConnectionInfo Shop = new("ShopRepo · shop", "localhost", 5433, "shop", "postgres", "p@ss=w0rd");

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
        var json = DbToolLauncher.PgAdminServersJson(Shop);

        Assert.Contains("\"Host\":\"localhost\"", json);
        Assert.Contains("\"Port\":5433", json);
        Assert.DoesNotContain("p@ss", json);
        Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Open_DBeaverInstalled_StartsItWithTheConnection()
    {
        var dbeaver = _programs.WriteFile("DBeaver/dbeaver.exe", "");

        var result = CreateLauncher().Open(DbTool.DBeaver, Shop);

        Assert.True(result.Success);
        var started = Assert.Single(_processes.Started);
        Assert.Equal(dbeaver, started.Executable);
        Assert.Equal("-con", started.Arguments[0]);
    }

    [Fact]
    public void Open_ToolNotInstalled_SaysSoAndStartsNothing()
    {
        var result = CreateLauncher().Open(DbTool.DbVisualizer, Shop);

        Assert.Equal((false, "DbVisualizer isn't installed on this PC."), (result.Success, result.Message));
        Assert.Empty(_processes.Started);
    }

    [Fact]
    public void Open_PgAdmin_ImportsTheServerThenDeletesTheTemporaryFile()
    {
        var pgAdmin = _programs.WriteFile("pgAdmin 4/runtime/pgAdmin4.exe", "");
        var python = _programs.WriteFile("pgAdmin 4/python/python.exe", "");
        _programs.WriteFile("pgAdmin 4/web/setup.py", "");

        var result = CreateLauncher().Open(DbTool.PgAdmin, Shop);

        Assert.True(result.Success);
        var import = Assert.Single(_processes.Ran);
        Assert.Equal(python, import.Executable);
        Assert.Equal(["load-servers", "--user"], new[] { import.Arguments[1], import.Arguments[3] });
        Assert.False(File.Exists(import.Arguments[2]));
        Assert.Equal(pgAdmin, Assert.Single(_processes.Started).Executable);
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

    private DbToolLauncher CreateLauncher() => new(new DbToolLocator([_programs.Path]), _processes);

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public List<(string Executable, IReadOnlyList<string> Arguments)> Started { get; } = [];

        public List<(string Executable, IReadOnlyList<string> Arguments)> Ran { get; } = [];

        public void Start(string executablePath, IReadOnlyList<string> arguments) => Started.Add((executablePath, arguments));

        public int Run(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Ran.Add((executablePath, arguments));
            Assert.True(File.Exists(arguments[2])); // the import file exists while pgAdmin reads it
            return 0;
        }
    }
}

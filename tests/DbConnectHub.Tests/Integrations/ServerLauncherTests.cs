using DbConnectHub.Integrations;

namespace DbConnectHub.Tests.Integrations;

public sealed class ServerLauncherTests
{
    private const string Folder = @"C:\Users\you\Projects\ShopRepo\shop_frontend";

    [Fact]
    public void CreateStartInfo_RunsTheCommandInAVisibleWindowInTheAppFolder()
    {
        var startInfo = ServerLauncher.CreateStartInfo("pnpm", ["run", "dev"], Folder)!;

        Assert.Equal(Path.Combine(Environment.SystemDirectory, "cmd.exe"), startInfo.FileName);
        Assert.Equal(["/k", "pnpm", "run", "dev"], startInfo.ArgumentList);
        Assert.Equal(Folder, startInfo.WorkingDirectory);
        Assert.False(startInfo.CreateNoWindow);
        Assert.Empty(startInfo.Arguments);
    }

    [Fact]
    public void CreateStartInfo_LooksUpTheProgramOnPathOnlyNeverInTheProjectFolder()
    {
        // Some shells (this test's runner among them) already set it; the server window must get it either way.
        const string variable = "NoDefaultCurrentDirectoryInExePath";
        var inherited = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, null);
        try
        {
            var startInfo = ServerLauncher.CreateStartInfo("npm", ["run", "dev"], Folder)!;

            Assert.Equal("1", startInfo.Environment[variable]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, inherited);
        }
    }

    [Theory]
    [InlineData("dev & del *")]
    [InlineData("dev\"")]
    [InlineData("%PATH%")]
    [InlineData("dev|more")]
    [InlineData("")]
    public void CreateStartInfo_WordThatCmdWouldRead_IsRefused(string script)
    {
        Assert.Null(ServerLauncher.CreateStartInfo("npm", ["run", script], Folder));
        Assert.False(ServerLauncher.Start("npm", ["run", script], Folder).Success);
    }

    [Fact]
    public void Start_FolderThatIsGone_SaysSoAndStartsNothing()
    {
        var launch = ServerLauncher.Start("npm", ["run", "dev"], Path.Combine(Path.GetTempPath(), "dbconnecthub-missing", Guid.NewGuid().ToString("N")));

        Assert.False(launch.Success);
        Assert.Contains("doesn't exist", launch.Message);
    }
}

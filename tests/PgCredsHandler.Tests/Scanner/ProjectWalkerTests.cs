using PgCredsHandler.Core.Models;
using PgCredsHandler.Scanner;
using PgCredsHandler.Tests.Support;

namespace PgCredsHandler.Tests.Scanner;

public sealed class ProjectWalkerTests : IDisposable
{
    private readonly TempFolder _root = new();

    public void Dispose() => _root.Dispose();

    [Theory]
    [InlineData("node_modules/pkg/.env")]
    [InlineData("AppData/Roaming/tool/.env")]
    [InlineData(".hidden/.env")]
    [InlineData("$WinREAgent/.env")]
    public void Walk_SkipsExcludedAndSystemFolders(string relativePath)
    {
        _root.WriteFile(relativePath, "DB_HOST=localhost");

        Assert.Empty(Walk());
    }

    [Fact]
    public void Walk_ConfigFileInSubfolder_ReportsTheNearestProjectRoot()
    {
        _root.WriteFile("shop/package.json", "{}");
        _root.WriteFile("shop/api/config/.env", "DB_HOST=localhost");

        var found = Assert.Single(Walk());

        Assert.Equal(_root.Combine("shop"), found.ProjectRoot);
    }

    [Fact]
    public void Walk_AppSettingsBesideACsproj_BelongsToThatDotNetProject()
    {
        Directory.CreateDirectory(_root.Combine("crm", ".git"));
        _root.WriteFile("crm/Crm.Api/Crm.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        _root.WriteFile("crm/Crm.Api/.env", "DB_HOST=localhost");

        Assert.Equal(_root.Combine("crm", "Crm.Api"), Assert.Single(Walk()).ProjectRoot);
    }

    [Fact]
    public void Walk_NoProjectMarker_UsesTheFilesOwnFolder()
    {
        _root.WriteFile("loose/.env", "DB_HOST=localhost");

        Assert.Equal(_root.Combine("loose"), Assert.Single(Walk()).ProjectRoot);
    }

    private List<FoundFile> Walk() =>
        new ProjectWalker(AppSettings.DefaultExcludedFolders())
            .Walk(_root.Path, name => name == ".env")
            .ToList();
}

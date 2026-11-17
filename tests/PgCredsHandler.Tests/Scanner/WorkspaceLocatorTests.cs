using PgCredsHandler.Scanner;
using PgCredsHandler.Tests.Support;

namespace PgCredsHandler.Tests.Scanner;

public sealed class WorkspaceLocatorTests : IDisposable
{
    private readonly TempFolder _root = new();

    public void Dispose() => _root.Dispose();

    [Fact]
    public void FindWorkspacePath_AppInsideARepository_ReturnsTheRepository()
    {
        Directory.CreateDirectory(_root.Combine("ShopRepo", ".git"));
        Directory.CreateDirectory(_root.Combine("ShopRepo", "shop_backend"));

        var workspace = new WorkspaceLocator().FindWorkspacePath(_root.Combine("ShopRepo", "shop_backend"), _root.Path);

        Assert.Equal(_root.Combine("ShopRepo"), workspace);
    }

    [Fact]
    public void FindWorkspacePath_NoRepository_ReturnsTheAppItself()
    {
        Directory.CreateDirectory(_root.Combine("loose", "api"));

        var appPath = _root.Combine("loose", "api");

        Assert.Equal(appPath, new WorkspaceLocator().FindWorkspacePath(appPath, _root.Path));
    }

    [Fact]
    public void FindWorkspacePath_NeverLooksAboveTheScanRoot()
    {
        Directory.CreateDirectory(_root.Combine(".git"));
        Directory.CreateDirectory(_root.Combine("projects", "api"));

        var appPath = _root.Combine("projects", "api");

        Assert.Equal(appPath, new WorkspaceLocator().FindWorkspacePath(appPath, _root.Combine("projects")));
    }
}

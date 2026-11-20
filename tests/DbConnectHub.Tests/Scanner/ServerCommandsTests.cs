using DbConnectHub.Core.Models;
using DbConnectHub.Scanner;
using DbConnectHub.Tests.Support;

namespace DbConnectHub.Tests.Scanner;

public sealed class ServerCommandsTests : IDisposable
{
    private readonly TempFolder _projects = new();

    public void Dispose() => _projects.Dispose();

    [Fact]
    public void Find_NodeAppWithoutALockFile_RunsItsDevScriptWithNpm()
    {
        _projects.WriteFile("ShopRepo/shop_frontend/package.json", """{ "scripts": { "dev": "next dev", "start": "next start" } }""");

        var command = ServerCommands.Find(App("ShopRepo/shop_frontend", "Next.js"))!;

        Assert.Equal(("npm run dev", _projects.Combine("ShopRepo", "shop_frontend")), (command.Text, command.Folder));
    }

    [Fact]
    public void Find_MonorepoWithItsLockFileAtTheRoot_UsesThatPackageManager()
    {
        _projects.WriteFile("ShopRepo/pnpm-lock.yaml", "lockfileVersion: '9.0'\n");
        _projects.WriteFile("ShopRepo/apps/shop_web/package.json", """{ "scripts": { "dev": "vite" } }""");

        Assert.Equal("pnpm run dev", ServerCommands.Find(App("ShopRepo/apps/shop_web", "Vite"))!.Text);
    }

    [Fact]
    public void Find_OnlyAStartScript_RunsThat()
    {
        _projects.WriteFile("ShopRepo/shop_api/yarn.lock", "");
        _projects.WriteFile("ShopRepo/shop_api/package.json", """{ "scripts": { "start": "node server.js" } }""");

        Assert.Equal("yarn run start", ServerCommands.Find(App("ShopRepo/shop_api", "Express"))!.Text);
    }

    [Fact]
    public void Find_AspNetCoreApp_RunsDotnetRun()
    {
        _projects.WriteFile("ShopRepo/ShopApi/ShopApi.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");

        Assert.Equal("dotnet run", ServerCommands.Find(App("ShopRepo/ShopApi", AppDetector.AspNetCore))!.Text);
    }

    [Fact]
    public void Find_NoServerScriptOrFolderGone_FindsNothing()
    {
        _projects.WriteFile("ShopRepo/shop_lib/package.json", """{ "scripts": { "build": "tsc" } }""");

        Assert.Null(ServerCommands.Find(App("ShopRepo/shop_lib", "Node.js")));
        Assert.Null(ServerCommands.Find(App("ShopRepo/deleted_app", "Next.js")));
    }

    private SavedApp App(string relativeFolder, string framework) => new()
    {
        WorkspaceName = "ShopRepo",
        WorkspacePath = _projects.Combine("ShopRepo"),
        AppPath = _projects.Combine(relativeFolder.Split('/')),
        Framework = framework,
    };
}

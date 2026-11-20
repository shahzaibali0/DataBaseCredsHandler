using System.Text;
using DbConnectHub.Core.Models;
using DbConnectHub.Scanner;
using DbConnectHub.Storage;
using DbConnectHub.Tests.Support;

namespace DbConnectHub.Tests.Scanner;

public sealed class ServerPortsTests : IDisposable
{
    private readonly TempFolder _projects = new();
    private readonly TempFolder _appData = new();
    private readonly JsonInventoryRepository _inventory;

    public ServerPortsTests() => _inventory = new JsonInventoryRepository(_appData.Combine("inventory.json"));

    public void Dispose()
    {
        _projects.Dispose();
        _appData.Dispose();
    }

    [Fact]
    public void Change_PortInDevScript_ReplacesOnlyThatNumber()
    {
        const string packageJson = """
            {
              "scripts": {   "dev":"next dev --port 3001 --turbopack", "lint": "next lint" },
              "dependencies": { "next": "16" }
            }
            """;
        var file = _projects.WriteFile("ShopRepo/shop_frontend/package.json", packageJson);
        var app = SaveApp("ShopRepo/shop_frontend", "Next.js", "http://localhost:3001");

        var change = Ports().Change(app, 4000);

        Assert.True(change.IsPossible, change.Message);
        Assert.Equal(packageJson.Replace("3001", "4000"), File.ReadAllText(file));
        Assert.Equal(("http://localhost:4000", "package.json dev script"), StoredAddress());
    }

    [Fact]
    public void Change_PortInEnvFile_KeepsEveryOtherByteOfIt()
    {
        _projects.WriteFile("ShopRepo/shop_api/package.json", """{ "scripts": { "dev": "nest start --watch" }, "dependencies": { "@nestjs/core": "11" } }""");
        var original = "# made-up values\r\nDATABASE_URL=postgresql://shop_user:secret-db-1@localhost:5432/shop_db\r\nPORT=3000\r\nAPI_KEY=not-a-real-key\r\n";
        var envFile = _projects.Combine("ShopRepo", "shop_api", ".env");
        File.WriteAllBytes(envFile, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(original)]);
        var app = SaveApp("ShopRepo/shop_api", "NestJS", "http://localhost:3000");

        var change = Ports().Change(app, 4100);

        Assert.True(change.IsPossible, change.Message);
        Assert.Equal([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(original.Replace("PORT=3000", "PORT=4100"))], File.ReadAllBytes(envFile));
        Assert.Equal("Done. Changes PORT=3000 to PORT=4100 in .env. Restart the server for it to take effect.", change.Message);
        Assert.Equal("http://localhost:4100", StoredAddress().Url);
    }

    [Fact]
    public void Change_FrameworkDefault_AddsThePortOptionToAPlainScript()
    {
        var file = _projects.WriteFile("ShopRepo/shop_web/package.json", """{ "scripts": { "dev": "vite" }, "devDependencies": { "vite": "7" } }""");
        var app = SaveApp("ShopRepo/shop_web", "Vite", "http://localhost:5173");

        var change = Ports().Change(app, 5200);

        Assert.True(change.IsPossible, change.Message);
        Assert.Equal("""{ "scripts": { "dev": "vite --port 5200" }, "devDependencies": { "vite": "7" } }""", File.ReadAllText(file));
        Assert.Equal(("http://localhost:5200", "package.json dev script"), StoredAddress());
    }

    [Fact]
    public void Preview_ScriptRunningSeveralCommands_RefusesAndWritesNothing()
    {
        const string packageJson = """{ "scripts": { "dev": "concurrently \"next dev\" \"tsc -w\"" }, "dependencies": { "next": "16" } }""";
        var file = _projects.WriteFile("ShopRepo/shop_frontend/package.json", packageJson);
        var app = SaveApp("ShopRepo/shop_frontend", "Next.js", "http://localhost:3000");

        var change = Ports().Change(app, 4000);

        Assert.False(change.IsPossible);
        Assert.Contains("more than one command", change.Message);
        Assert.Equal(packageJson, File.ReadAllText(file));
    }

    [Fact]
    public void Preview_BackendOnItsDefaultPort_SaysTheCodeSetsIt()
    {
        _projects.WriteFile("ShopRepo/shop_api/package.json", """{ "scripts": { "dev": "node server.js" }, "dependencies": { "express": "5" } }""");

        var change = ServerPorts.Preview(SaveApp("ShopRepo/shop_api", "Express", "http://localhost:3000"), 4000);

        Assert.Equal((false, "This Express server takes its port from its own code, so change it there."), (change.IsPossible, change.Message));
    }

    [Fact]
    public void Change_LaunchSettings_MovesEveryProfileOnThatAddressAndNoOtherPort()
    {
        _projects.WriteFile("ShopRepo/ShopApi/ShopApi.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>""");
        const string settings = """
            { "profiles": {
              "http": { "applicationUrl": "http://localhost:5001" },
              "https": { "applicationUrl": "https://localhost:7001;http://localhost:5001" },
              "other": { "applicationUrl": "http://localhost:50010" } } }
            """;
        var file = _projects.WriteFile("ShopRepo/ShopApi/Properties/launchSettings.json", settings);
        var app = SaveApp("ShopRepo/ShopApi", AppDetector.AspNetCore, "http://localhost:5001");

        var change = Ports().Change(app, 5080);

        Assert.True(change.IsPossible, change.Message);
        Assert.Equal(settings.Replace("localhost:5001\"", "localhost:5080\""), File.ReadAllText(file));
        Assert.Equal(("http://localhost:5080", "launchSettings.json"), StoredAddress());
    }

    [Theory]
    [InlineData(80, "Choose a port from 1024 to 65535.")]
    [InlineData(70000, "Choose a port from 1024 to 65535.")]
    [InlineData(3001, "The server already runs on 3001.")]
    public void Preview_PortThatCantBeUsed_SaysWhy(int port, string expected)
    {
        _projects.WriteFile("ShopRepo/shop_frontend/package.json", """{ "scripts": { "dev": "next dev -p 3001" }, "dependencies": { "next": "16" } }""");

        var change = ServerPorts.Preview(SaveApp("ShopRepo/shop_frontend", "Next.js", "http://localhost:3001"), port);

        Assert.Equal((false, expected), (change.IsPossible, change.Message));
    }

    [Fact]
    public void Change_EnvFileThatIsntValidUtf8_IsLeftAlone()
    {
        _projects.WriteFile("ShopRepo/shop_api/package.json", """{ "scripts": { "dev": "nest start" }, "dependencies": { "@nestjs/core": "11" } }""");
        byte[] original = [.. Encoding.ASCII.GetBytes("NAME=caf"), 0xE9, .. Encoding.ASCII.GetBytes("\nPORT=3000\n")]; // é in Latin-1
        var envFile = _projects.Combine("ShopRepo", "shop_api", ".env");
        File.WriteAllBytes(envFile, original);

        var change = Ports().Change(SaveApp("ShopRepo/shop_api", "NestJS", "http://localhost:3000"), 4100);

        Assert.False(change.IsPossible);
        Assert.Equal(original, File.ReadAllBytes(envFile));
    }

    [Fact]
    public void Change_ScriptNameThatAppearsTwice_IsRefusedRatherThanGuessed()
    {
        const string packageJson = """{ "scripts": { "dev": "next dev -p 3001" }, "nodemonConfig": { "dev": "x" }, "dependencies": { "next": "16" } }""";
        var file = _projects.WriteFile("ShopRepo/shop_frontend/package.json", packageJson);

        var change = Ports().Change(SaveApp("ShopRepo/shop_frontend", "Next.js", "http://localhost:3001"), 4000);

        Assert.False(change.IsPossible);
        Assert.Equal(packageJson, File.ReadAllText(file));
    }

    [Fact]
    public void Change_LaunchUrlWithAPath_ChangesOnlyThePort()
    {
        _projects.WriteFile("ShopRepo/ShopApi/ShopApi.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web" />""");
        const string settings = """{ "profiles": { "http": { "launchUrl": "http://localhost:5001/swagger", "applicationUrl": "http://localhost:5001/" } } }""";
        var file = _projects.WriteFile("ShopRepo/ShopApi/Properties/launchSettings.json", settings);

        var change = Ports().Change(SaveApp("ShopRepo/ShopApi", AppDetector.AspNetCore, "http://localhost:5001"), 5080);

        Assert.True(change.IsPossible, change.Message);
        Assert.Equal("""{ "profiles": { "http": { "launchUrl": "http://localhost:5080/swagger", "applicationUrl": "http://localhost:5080/" } } }""",
            File.ReadAllText(file));
    }

    [Fact]
    public void Change_SucceedsWithoutLeavingATempFileBehind()
    {
        _projects.WriteFile("ShopRepo/shop_api/package.json", """{ "scripts": { "dev": "nest start" }, "dependencies": { "@nestjs/core": "11" } }""");
        _projects.WriteFile("ShopRepo/shop_api/.env.local", "PORT=3000\nAPI_KEY=not-a-real-key\n");

        var change = Ports().Change(SaveApp("ShopRepo/shop_api", "NestJS", "http://localhost:3000"), 4100);

        Assert.True(change.IsPossible, change.Message);
        Assert.Equal([".env.local", "package.json"],
            Directory.GetFiles(_projects.Combine("ShopRepo", "shop_api")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Change_TempFileNameAlreadyTakenAndCantBeCleared_RefusesAndKeepsTheEnvFile()
    {
        _projects.WriteFile("ShopRepo/shop_api/package.json", """{ "scripts": { "dev": "nest start" }, "dependencies": { "@nestjs/core": "11" } }""");
        var envFile = _projects.WriteFile("ShopRepo/shop_api/.env.local", "PORT=3000\n");
        Directory.CreateDirectory(_projects.Combine("ShopRepo", "shop_api", ".env.dbconnecthub-tmp.local")); // can't be deleted as a file

        var change = Ports().Change(SaveApp("ShopRepo/shop_api", "NestJS", "http://localhost:3000"), 4100);

        Assert.False(change.IsPossible);
        Assert.Contains(".env.dbconnecthub-tmp.local is in the way", change.Message);
        Assert.Equal("PORT=3000\n", File.ReadAllText(envFile));
    }

    [Fact]
    public void Preview_EnvFileThatIsALink_IsRefused()
    {
        _projects.WriteFile("ShopRepo/shop_api/package.json", """{ "scripts": { "dev": "nest start" }, "dependencies": { "@nestjs/core": "11" } }""");
        var sharedEnv = _projects.WriteFile("ShopRepo/shared.env", "PORT=3000\n");
        File.CreateSymbolicLink(_projects.Combine("ShopRepo", "shop_api", ".env"), sharedEnv);

        var change = Ports().Change(SaveApp("ShopRepo/shop_api", "NestJS", "http://localhost:3000"), 4100);

        Assert.Equal((false, ".env is a link to another file; change the port there by hand."), (change.IsPossible, change.Message));
        Assert.Equal("PORT=3000\n", File.ReadAllText(sharedEnv));
    }

    [Fact]
    public void Preview_EnvFileWithOldMacLineEndings_IsRefused()
    {
        _projects.WriteFile("ShopRepo/shop_api/package.json", """{ "scripts": { "dev": "nest start" }, "dependencies": { "@nestjs/core": "11" } }""");
        _projects.WriteFile("ShopRepo/shop_api/.env.local", "NAME=shop\rPORT=3001\r");
        _projects.WriteFile("ShopRepo/shop_api/.env", "PORT=3000\n");

        var change = ServerPorts.Preview(SaveApp("ShopRepo/shop_api", "NestJS", "http://localhost:3001"), 4100);

        Assert.Equal((false, ".env.local uses old Mac line endings; change its PORT line by hand."), (change.IsPossible, change.Message));
    }

    private ServerPorts Ports() => new(_inventory);

    private SavedApp SaveApp(string relativeFolder, string framework, string localUrl)
    {
        var app = new SavedApp
        {
            WorkspaceName = "ShopRepo",
            WorkspacePath = _projects.Combine("ShopRepo"),
            AppName = Path.GetFileName(relativeFolder),
            AppPath = _projects.Combine(relativeFolder.Split('/')),
            Framework = framework,
            LocalUrl = localUrl,
            LocalUrlSource = "before the change",
        };
        _inventory.Save(new InventorySnapshot { Apps = [app] });
        return app;
    }

    private (string? Url, string? Source) StoredAddress()
    {
        var stored = Assert.Single(_inventory.Load().Apps);
        return (stored.LocalUrl, stored.LocalUrlSource);
    }
}

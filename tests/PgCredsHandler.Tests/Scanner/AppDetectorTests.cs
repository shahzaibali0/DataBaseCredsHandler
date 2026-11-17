using PgCredsHandler.Core.Models;
using PgCredsHandler.Scanner;
using PgCredsHandler.Tests.Support;

namespace PgCredsHandler.Tests.Scanner;

public sealed class AppDetectorTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Detect_NextAppWithPortInDevScript_UsesThatPort()
    {
        var path = _folder.WriteFile("backend/package.json",
            """{ "scripts": { "dev": "next dev --port 3001" }, "dependencies": { "next": "16.0.0" } }""");

        var facts = AppDetector.Detect(path)!;

        Assert.Equal(("Next.js", "http://localhost:3001", "package.json dev script"), (facts.Framework, facts.LocalUrl, facts.LocalUrlSource));
    }

    [Fact]
    public void Detect_PortInEnvFile_UsesIt()
    {
        var path = _folder.WriteFile("api/package.json", """{ "scripts": { "dev": "nest start --watch" }, "dependencies": { "@nestjs/core": "11" } }""");
        _folder.WriteFile("api/.env", "PORT=4100\n");

        var facts = AppDetector.Detect(path)!;

        Assert.Equal(("NestJS", AppRole.Backend, "http://localhost:4100"), (facts.Framework, facts.RoleFromFramework, facts.LocalUrl));
    }

    [Fact]
    public void Detect_PortReferencingAnotherEnvValue_DoesNotReadIt()
    {
        var path = _folder.WriteFile("api/package.json",
            """{ "scripts": { "dev": "nest start --watch" }, "dependencies": { "@nestjs/core": "11" } }""");
        _folder.WriteFile("api/.env", "SECRET_PORT=4100\nPORT=${SECRET_PORT}\n");

        var facts = AppDetector.Detect(path)!;

        Assert.Equal(("http://localhost:3000", "NestJS default"), (facts.LocalUrl, facts.LocalUrlSource));
    }

    [Fact]
    public void Detect_ViteWithoutPort_UsesTheFrameworkDefault()
    {
        var path = _folder.WriteFile("web/package.json", """{ "scripts": { "dev": "vite" }, "devDependencies": { "vite": "6" } }""");

        var facts = AppDetector.Detect(path)!;

        Assert.Equal(("Vite", AppRole.Frontend, "http://localhost:5173", "Vite default"),
            (facts.Framework, facts.RoleFromFramework, facts.LocalUrl, facts.LocalUrlSource));
    }

    [Fact]
    public void Detect_NodeApp_ReportsStackAndKeyPackagesOnly()
    {
        var path = _folder.WriteFile("shop/package.json", """
            { "scripts": { "dev": "next dev" },
              "dependencies": { "next": "^16.0.1", "react": "19.1.0", "@prisma/client": "6", "next-auth": "5", "stripe": "17",
                                "@radix-ui/react-dialog": "1", "@radix-ui/react-tabs": "1", "clsx": "2" },
              "devDependencies": { "typescript": "5", "tailwindcss": "4", "@types/node": "22" } }
            """);

        var facts = AppDetector.Detect(path)!;

        Assert.Equal(["Next.js 16", "React 19", "TypeScript", "Node.js"], facts.Stack);
        Assert.Equal(
            [new KeyPackage("Auth", "NextAuth"), new KeyPackage("Database", "Prisma"), new KeyPackage("Payments", "Stripe"),
             new KeyPackage("UI", "Radix UI"), new KeyPackage("UI", "Tailwind CSS")],
            facts.Packages);
    }

    [Fact]
    public void Detect_AspNetProject_ReportsRuntimeAndPackages()
    {
        var path = _folder.WriteFile("Api/Api.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
                <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
                <PackageReference Include="Swashbuckle.AspNetCore" Version="6.0.0" />
              </ItemGroup>
            </Project>
            """);

        var facts = AppDetector.Detect(path)!;

        Assert.Equal(["ASP.NET Core", "C#", ".NET 8"], facts.Stack);
        Assert.Equal(
            [new KeyPackage("API docs", "Swagger"), new KeyPackage("Database", "EF Core (PostgreSQL)"), new KeyPackage("Monitoring", "Serilog")],
            facts.Packages);
    }

    [Fact]
    public void Detect_ToolingOnlyPackageJson_IsNotAnApp()
    {
        var path = _folder.WriteFile("package.json", """{ "devDependencies": { "prettier": "3" } }""");

        Assert.Null(AppDetector.Detect(path));
    }

    [Fact]
    public void Detect_AspNetProject_ReadsLaunchSettings()
    {
        var path = _folder.WriteFile("Api/Api.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web"></Project>""");
        _folder.WriteFile("Api/Properties/launchSettings.json",
            """{ "profiles": { "http": { "applicationUrl": "http://localhost:5080" }, "https": { "applicationUrl": "https://localhost:7080;http://localhost:5080" } } }""");

        var facts = AppDetector.Detect(path)!;

        Assert.Equal(("ASP.NET Core", "http://localhost:5080", "launchSettings.json"), (facts.Framework, facts.LocalUrl, facts.LocalUrlSource));
    }

    [Fact]
    public void Detect_ClassLibrary_IsNotAnApp()
    {
        var path = _folder.WriteFile("Core/Core.csproj", """<Project Sdk="Microsoft.NET.Sdk"></Project>""");

        Assert.Null(AppDetector.Detect(path));
    }

    [Theory]
    [InlineData("shop_backend", AppRole.App, AppRole.Backend)]
    [InlineData("store_frontend", AppRole.App, AppRole.Frontend)]
    [InlineData("api", AppRole.Frontend, AppRole.Backend)]
    [InlineData("shop", AppRole.Frontend, AppRole.Frontend)]
    public void ChooseRole_FolderNameWinsOverFramework(string appName, AppRole fromFramework, AppRole expected) =>
        Assert.Equal(expected, AppDetector.ChooseRole(appName, fromFramework));
}

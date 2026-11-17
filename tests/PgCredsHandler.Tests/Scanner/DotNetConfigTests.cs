using PgCredsHandler.Core.Models;
using PgCredsHandler.Scanner;
using PgCredsHandler.Scanner.Parsers;
using PgCredsHandler.Tests.Support;

namespace PgCredsHandler.Tests.Scanner;

/// <summary>How .NET projects keep their database settings: User Secrets, and ConnectionStrings__X variables.</summary>
public sealed class DotNetConfigTests : IDisposable
{
    private const string SecretsId = "11111111-2222-3333-4444-555555555555";

    private readonly TempFolder _projects = new();
    private readonly TempFolder _userSecrets = new();

    public void Dispose()
    {
        _projects.Dispose();
        _userSecrets.Dispose();
    }

    [Fact]
    public void Scan_ProjectUsingUserSecrets_FindsTheConnectionStoredOutsideTheProject()
    {
        WriteApiProject(userSecretsId: SecretsId);
        _userSecrets.WriteFile($"{SecretsId}/secrets.json", """
            { "ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5432;Database=shop_db;Username=postgres;Password=s3cret-dev" },
              "Payments": { "ApiKey": "pk-not-a-database" } }
            """);

        var found = Assert.Single(Scan().Connections);

        Assert.Equal(("shop_db", "postgres", "s3cret-dev", "user secrets"), (found.Database, found.Username, found.Password, found.SourceKind));
        Assert.Equal(_projects.Combine("ShopRepo", "Shop.Api"), found.ProjectPath);
    }

    [Fact]
    public void Scan_ProjectWithoutUserSecrets_ReadsNothingOutsideIt()
    {
        WriteApiProject(userSecretsId: null);
        _userSecrets.WriteFile($"{SecretsId}/secrets.json",
            """{ "ConnectionStrings": { "Main": "Host=localhost;Database=other_db;Username=app;Password=x-123" } }""");

        Assert.Empty(Scan().Connections);
    }

    [Fact]
    public void EnvFile_DotNetConnectionStringVariable_IsRead()
    {
        var vars = new Dictionary<string, string>
        {
            ["ConnectionStrings__DefaultConnection"] = "Host=localhost;Port=5433;Database=shop_db;Username=app;Password=pw-123",
        };

        var found = Assert.Single(EnvCredentialExtractor.Extract(vars));

        Assert.Equal("ConnectionStrings:DefaultConnection", found.SourceKey);
        Assert.Equal(("localhost", 5433, "shop_db", "app", "pw-123"),
            (found.Parts.Host, found.Parts.Port, found.Parts.Database, found.Parts.Username, found.Parts.Password));
    }

    [Theory]
    [InlineData("YOUR_POSTGRES_PASSWORD")]
    [InlineData("<password>")]
    [InlineData("changeme")]
    [InlineData("********")]
    public void Scan_TemplatePassword_IsNotTreatedAsAPassword(string templatePassword)
    {
        _projects.WriteFile("ShopRepo/api/package.json", "{}");
        _projects.WriteFile("ShopRepo/api/.env.example", $"PGHOST=localhost\nPGDATABASE=shop_db\nPGUSER=postgres\nPGPASSWORD={templatePassword}\n");

        var found = Assert.Single(Scan().Connections);

        Assert.Null(found.Password);
    }

    private void WriteApiProject(string? userSecretsId)
    {
        Directory.CreateDirectory(_projects.Combine("ShopRepo", ".git"));
        var secrets = userSecretsId is null ? "" : $"<UserSecretsId>{userSecretsId}</UserSecretsId>";
        _projects.WriteFile("ShopRepo/Shop.Api/Shop.Api.csproj",
            $"""<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net8.0</TargetFramework>{secrets}</PropertyGroup></Project>""");
        _projects.WriteFile("ShopRepo/Shop.Api/appsettings.json", """{ "Logging": { "LogLevel": { "Default": "Information" } } }""");
    }

    private ScanFindings Scan() => new ProjectScanner(new AppSettings(), userSecretsRoot: _userSecrets.Path).Scan([_projects.Path]);
}

using DbConnectHub.Core.Models;
using DbConnectHub.Storage;
using DbConnectHub.Tests.Support;

namespace DbConnectHub.Tests.Storage;

public sealed class LegacyDataMigrationTests : IDisposable
{
    private readonly TempFolder _appData = new();
    private readonly InMemoryCredentialStore _oldCredentials = new();
    private readonly InMemoryCredentialStore _newCredentials = new();

    public void Dispose() => _appData.Dispose();

    private string OldFolder => _appData.Combine("PgCredsHandler");

    private string NewFolder => _appData.Combine("DbConnectHub");

    [Fact]
    public void Run_OldDataExists_MovesFilesAndPasswordsToTheNewName()
    {
        var saved = new SavedConnection { Username = "app", HasPassword = true };
        new JsonConnectionRepository(Path.Combine(OldFolder, "connections.json")).SaveAll([saved]);
        _appData.WriteFile("PgCredsHandler/settings.json", "{}");
        _oldCredentials.Save(saved.Id, "app", "s3cret-dev");

        var result = LegacyDataMigration.Run(OldFolder, NewFolder, _oldCredentials, _newCredentials);

        Assert.Equal(new MigrationResult(FilesCopied: 2, PasswordsMoved: 1), result);
        Assert.Equal(saved.Id, Assert.Single(new JsonConnectionRepository(Path.Combine(NewFolder, "connections.json")).GetAll()).Id);
        Assert.Equal("s3cret-dev", _newCredentials.Read(saved.Id));
        Assert.Null(_oldCredentials.Read(saved.Id));
    }

    [Fact]
    public void Run_NewDataAlreadyThere_ChangesNothing()
    {
        _appData.WriteFile("PgCredsHandler/connections.json", "[]");
        _appData.WriteFile("DbConnectHub/connections.json", """[{ "id": "keep" }]""");

        var result = LegacyDataMigration.Run(OldFolder, NewFolder, _oldCredentials, _newCredentials);

        Assert.Equal(new MigrationResult(0, 0), result);
        Assert.Equal("""[{ "id": "keep" }]""", File.ReadAllText(Path.Combine(NewFolder, "connections.json")));
    }

    [Fact]
    public void Run_NoOldData_DoesNothing()
    {
        Assert.Equal(new MigrationResult(0, 0), LegacyDataMigration.Run(OldFolder, NewFolder, _oldCredentials, _newCredentials));
        Assert.False(Directory.Exists(NewFolder));
    }
}

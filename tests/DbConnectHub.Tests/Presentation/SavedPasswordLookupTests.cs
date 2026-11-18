using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;
using DbConnectHub.Presentation;
using DbConnectHub.Storage;
using DbConnectHub.Tests.Support;

namespace DbConnectHub.Tests.Presentation;

public sealed class SavedPasswordLookupTests : IDisposable
{
    private readonly TempFolder _appData = new();
    private readonly InMemoryCredentialStore _credentials = new();

    public void Dispose() => _appData.Dispose();

    [Fact]
    public void FindPassword_SavedLogin_ReadsItsPasswordFromTheCredentialStore()
    {
        Save(new SavedConnection { Host = "localhost", Port = 5433, Database = "shop_db", Username = "shop_user", HasPassword = true }, "secret-db-1");

        var password = SavedPasswordLookup.FindPassword(Stores(), new DatabaseLogin("LOCALHOST", 5433, "shop_user", "shop_db"));

        Assert.Equal("secret-db-1", password);
    }

    [Fact]
    public void FindPassword_StaleAndCurrentEntryForTheLogin_PrefersTheCurrentOne()
    {
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "shop_db", Username = "app", HasPassword = true, IsStale = true }, "old-secret");
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "shop_db", Username = "app", HasPassword = true }, "current-secret");

        Assert.Equal("current-secret", SavedPasswordLookup.FindPassword(Stores(), new DatabaseLogin("localhost", 5432, "app", "shop_db")));
    }

    [Fact]
    public void FindPassword_SameLoginOtherDatabases_PicksTheOneForThisDatabase()
    {
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "shop_db", Username = "postgres", HasPassword = true }, "shop-secret");
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "blog_db", Username = "postgres", HasPassword = true }, "blog-secret");

        Assert.Equal("blog-secret", SavedPasswordLookup.FindPassword(Stores(), new DatabaseLogin("localhost", 5432, "postgres", "blog_db")));
    }

    // Two projects with a database of the same name and different passwords: guessing would send one project's
    // password to the other's server, so pgAdmin is left to ask.
    [Fact]
    public void FindPassword_SameLoginWithDifferentPasswords_FindsNothing()
    {
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "shop_db", Username = "postgres", HasPassword = true }, "shop-secret");
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "shop_db", Username = "postgres", HasPassword = true }, "blog-secret");

        Assert.Null(SavedPasswordLookup.FindPassword(Stores(), new DatabaseLogin("localhost", 5432, "postgres", "shop_db")));
    }

    [Fact]
    public void FindPassword_SameLoginWithTheSamePasswordTwice_FindsIt()
    {
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "shop_db", Username = "postgres", HasPassword = true }, "shared-secret");
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "shop_db", Username = "postgres", HasPassword = true }, "shared-secret");

        Assert.Equal("shared-secret", SavedPasswordLookup.FindPassword(Stores(), new DatabaseLogin("localhost", 5432, "postgres", "shop_db")));
    }

    [Fact]
    public void FindPassword_OtherPortOrUser_FindsNothing()
    {
        Save(new SavedConnection { Host = "localhost", Port = 5432, Database = "shop_db", Username = "app", HasPassword = true }, "secret-db-1");

        Assert.Null(SavedPasswordLookup.FindPassword(Stores(), new DatabaseLogin("localhost", 5433, "app", "shop_db")));
        Assert.Null(SavedPasswordLookup.FindPassword(Stores(), new DatabaseLogin("localhost", 5432, "App", "shop_db")));
    }

    private void Save(SavedConnection connection, string password)
    {
        var repository = new JsonConnectionRepository(_appData.Combine("connections.json"));
        repository.SaveAll([.. repository.GetAll(), connection]);
        _credentials.Save(connection.Id, connection.Username, password);
    }

    private DataStores Stores() => new(
        new JsonConnectionRepository(_appData.Combine("connections.json")), _credentials,
        new JsonSettingsStore(_appData.Combine("settings.json")), new JsonInventoryRepository(_appData.Combine("inventory.json")));
}

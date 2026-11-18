using DbConnectHub.Core.Models;

namespace DbConnectHub.Tests.Util;

public sealed class DatabaseLoginTests
{
    [Fact]
    public void ToKey_ThenTryParseKey_GivesTheSameLoginBack()
    {
        var login = new DatabaseLogin("db.example.com", 6543, "shop user", "shop_db");

        Assert.True(DatabaseLogin.TryParseKey(login.ToKey(), out var parsed));
        Assert.Equal(login, parsed);
    }

    // The key goes into a command pgAdmin runs through cmd.exe, so a host or user name from a config file must not
    // be able to add shell syntax to it.
    [Fact]
    public void ToKey_HostOrUserWithShellCharacters_IsOnlyHexDigits()
    {
        var key = new DatabaseLogin("localhost & calc", 5432, "\"admin\" | del", "db & exit").ToKey();

        Assert.Matches("^[0-9a-f]+$", key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-hex")]
    [InlineData("6c6f63616c686f7374")] // "localhost" with no port, user or database
    public void TryParseKey_AnythingElse_IsRefused(string key)
    {
        Assert.False(DatabaseLogin.TryParseKey(key, out _));
    }
}

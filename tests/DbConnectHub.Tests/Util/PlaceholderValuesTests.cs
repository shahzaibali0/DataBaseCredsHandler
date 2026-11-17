using DbConnectHub.Core.Util;

namespace DbConnectHub.Tests.Util;

public sealed class PlaceholderValuesTests
{
    [Theory]
    [InlineData("HOST:PORT", "DATABASE", "USER")]       // postgresql://USER:PASSWORD@HOST:PORT/DATABASE
    [InlineData("host:port", "dbname", "user")]         // postgresql://user:password@host:port/dbname
    [InlineData("host", "shop", "app")]
    [InlineData("your-host", "shop", "app")]
    [InlineData("localhost", "dbname", "user")]
    [InlineData("localhost", "database", "username")]
    [InlineData("<host>", "shop", "app")]
    [InlineData("localhost", "<database>", "app")]
    [InlineData("localhost", "shop", "{user}")]
    public void LooksLikePlaceholder_TemplateValues_ReturnsTrue(string host, string database, string username)
    {
        Assert.True(PlaceholderValues.LooksLikePlaceholder(host, database, username));
    }

    [Theory]
    [InlineData("localhost", "shop", "postgres")]
    [InlineData("::1", "shop", "app")]                  // IPv6 has colons but is a real host
    [InlineData("db", "shop", "app")]                   // a docker-compose service name
    [InlineData("crm-db.example.com", "crm", "crm_app")]
    [InlineData("localhost", "database", "postgres")]   // a real database can be called "database"
    [InlineData("localhost", "shop", "user")]           // or a real user "user"
    public void LooksLikePlaceholder_RealValues_ReturnsFalse(string host, string database, string username)
    {
        Assert.False(PlaceholderValues.LooksLikePlaceholder(host, database, username));
    }
}

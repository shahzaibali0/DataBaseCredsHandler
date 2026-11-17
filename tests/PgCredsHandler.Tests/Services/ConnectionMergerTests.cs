using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Services;

namespace PgCredsHandler.Tests.Services;

public sealed class ConnectionMergerTests
{
    private const string ProjectPath = @"C:\Users\you\Projects\shop";
    private const string ScanRoot = @"C:\Users\you\Projects";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Merge_NewConnection_AddsItAndQueuesItsPassword()
    {
        var merge = Merge([], [Discovered(password: "secret")]);

        var added = Assert.Single(merge.Connections);
        Assert.Equal(1, merge.Added);
        Assert.True(added.HasPassword);
        Assert.Equal((added.Id, "app", "secret"), Assert.Single(merge.PasswordWrites));
    }

    [Fact]
    public void Merge_ManualEntryWithSameIdentity_IsLeftUntouched()
    {
        var manual = Saved(isManual: true);
        manual.Notes = "edited by hand";

        var merge = Merge([manual], [Discovered(password: "other")]);

        var kept = Assert.Single(merge.Connections);
        Assert.Equal("edited by hand", kept.Notes);
        Assert.Empty(merge.PasswordWrites);
    }

    [Fact]
    public void Merge_ConnectionNoLongerFound_IsMarkedStaleNotDeleted()
    {
        var merge = Merge([Saved()], []);

        Assert.True(Assert.Single(merge.Connections).IsStale);
        Assert.Equal(1, merge.MarkedStale);
    }

    [Fact]
    public void Merge_ConnectionOutsideTheScannedRoots_IsNotMarkedStale()
    {
        var elsewhere = Saved();
        elsewhere.ProjectPath = @"D:\Other\api";

        var merge = Merge([elsewhere], []);

        Assert.False(Assert.Single(merge.Connections).IsStale);
    }

    [Fact]
    public void Merge_HiddenFingerprint_IsNotAddedBack()
    {
        var discovered = Discovered(password: "secret");

        var merge = ConnectionMerger.Merge([], [discovered], [ScanRoot], [discovered.Fingerprint], _ => null, Now);

        Assert.Empty(merge.Connections);
        Assert.Empty(merge.PasswordWrites);
    }

    [Fact]
    public void Merge_PasswordChangedInConfig_QueuesWriteAndMarksTheChange()
    {
        var saved = Saved();
        saved.HasPassword = true;

        var merge = ConnectionMerger.Merge([saved], [Discovered(password: "new")], [ScanRoot], [], _ => "old", Now);

        Assert.Equal(Now, Assert.Single(merge.Connections).PasswordChangedAt);
        Assert.Equal(1, merge.PasswordsChanged);
        Assert.Equal("new", Assert.Single(merge.PasswordWrites).Password);
    }

    [Fact]
    public void Merge_DiscoveredWithoutPassword_KeepsTheStoredOne()
    {
        var saved = Saved();
        saved.HasPassword = true;

        var merge = Merge([saved], [Discovered(password: null)]);

        Assert.True(Assert.Single(merge.Connections).HasPassword);
        Assert.Empty(merge.PasswordWrites);
    }

    [Fact]
    public void Merge_SameDatabaseInSeveralFiles_KeepsOneEntryPreferringTheRealEnvFile()
    {
        var example = Discovered(password: "example", sourceFile: ".env.example", isExample: true);
        var real = Discovered(password: "real", sourceFile: ".env");

        var merge = Merge([], [example, real]);

        Assert.Single(merge.Connections);
        Assert.Equal("real", Assert.Single(merge.PasswordWrites).Password);
    }

    [Fact]
    public void Merge_NeverChangesTheListItWasGiven()
    {
        var saved = Saved();

        Merge([saved], []);

        Assert.False(saved.IsStale);
    }

    private static MergeResult Merge(IReadOnlyList<SavedConnection> existing, IReadOnlyList<DiscoveredConnection> found) =>
        ConnectionMerger.Merge(existing, found, [ScanRoot], [], _ => null, Now);

    private static SavedConnection Saved(bool isManual = false) => new()
    {
        ProjectName = "shop", ProjectPath = ProjectPath, Host = "localhost", Port = 5432,
        Database = "shop", Username = "app", IsManual = isManual,
    };

    private static DiscoveredConnection Discovered(string? password, string sourceFile = ".env", bool isExample = false) => new()
    {
        ProjectName = "shop", ProjectPath = ProjectPath, SourceFile = Path.Combine(ProjectPath, sourceFile),
        SourceKind = ".env", IsExampleFile = isExample, Host = "localhost", Port = 5432,
        Database = "shop", Username = "app", Password = password,
    };
}

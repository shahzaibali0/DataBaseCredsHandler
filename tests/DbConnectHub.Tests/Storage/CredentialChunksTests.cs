using System.Text;
using DbConnectHub.Storage;

namespace DbConnectHub.Tests.Storage;

public sealed class CredentialChunksTests
{
    [Fact]
    public void Split_SmallValue_IsOnePiece()
    {
        var bytes = Encoding.Unicode.GetBytes("short-secret");

        Assert.Single(CredentialChunks.Split(bytes));
    }

    [Fact]
    public void Split_LongPassword_FitsEveryPieceInOneCredential()
    {
        var bytes = Encoding.Unicode.GetBytes(new string('k', 3000)); // 6,000 bytes: too big for one credential

        var chunks = CredentialChunks.Split(bytes);

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 2560));
    }

    [Fact]
    public void SplitThenJoin_GivesBackTheSameText()
    {
        var value = string.Concat(Enumerable.Repeat("ÄbC💥-password-", 900));

        var joined = CredentialChunks.Join(CredentialChunks.Split(Encoding.Unicode.GetBytes(value)));

        Assert.Equal(value, Encoding.Unicode.GetString(joined));
    }

    [Fact]
    public void TargetFor_FirstPieceKeepsThePlainName()
    {
        Assert.Equal("DbConnectHub:abc", CredentialChunks.TargetFor("DbConnectHub:abc", 0));
        Assert.Equal("DbConnectHub:abc#2", CredentialChunks.TargetFor("DbConnectHub:abc", 2));
    }

    [Fact]
    public void TargetsFrom_MissingSuffixCannotHideLaterChunksFromCleanup()
    {
        var targets = CredentialChunks.TargetsFrom("DbConnectHub:abc", 1).ToList();

        Assert.Equal(CredentialChunks.MaxChunks - 1, targets.Count);
        Assert.Contains("DbConnectHub:abc#2", targets);
        Assert.Equal("DbConnectHub:abc#63", targets[^1]);
    }
}

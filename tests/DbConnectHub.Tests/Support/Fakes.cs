using DbConnectHub.Core.Abstractions;

namespace DbConnectHub.Tests.Support;

/// <summary>A credential store that refuses every write, like a Credential Manager that is unavailable.</summary>
public sealed class RefusingCredentialStore : ICredentialStore
{
    public void Save(string connectionId, string username, string password) =>
        throw new InvalidOperationException("Credential store unavailable");

    public string? Read(string connectionId) => null;

    public void Delete(string connectionId) { }
}

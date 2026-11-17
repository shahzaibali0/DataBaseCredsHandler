using System.Runtime.InteropServices;
using PgCredsHandler.Core.Abstractions;

namespace PgCredsHandler.Storage;

public static class CredentialStoreFactory
{
    /// <summary>
    /// Returns the Windows Credential Manager store on Windows, and an in-memory store elsewhere
    /// (used only so the solution builds and unit-tests run on Linux CI — never in the shipped app).
    /// </summary>
    public static ICredentialStore Create() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new WindowsCredentialStore()
            : new InMemoryCredentialStore();
}

/// <summary>Non-Windows fallback. Not secure; exists only for cross-platform build/test.</summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _store = new();
    public void Save(string connectionId, string username, string password) => _store[connectionId] = password;
    public string? Read(string connectionId) => _store.GetValueOrDefault(connectionId);
    public void Delete(string connectionId) => _store.Remove(connectionId);

    /// <summary>Lets tests assert that an out-of-scope value was never stored without exposing stored values.</summary>
    public bool ContainsValue(string value) => _store.Values.Contains(value, StringComparer.Ordinal);
}

using PgCredsHandler.Core.Models;

namespace PgCredsHandler.Core.Abstractions;

/// <summary>Stores passwords securely (Windows Credential Manager in the real app).</summary>
public interface ICredentialStore
{
    void Save(string connectionId, string username, string password);
    string? Read(string connectionId);
    void Delete(string connectionId);
}

/// <summary>Stores connection metadata (no passwords).</summary>
public interface IConnectionRepository
{
    IReadOnlyList<SavedConnection> GetAll();
    void SaveAll(IEnumerable<SavedConnection> connections);
}

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

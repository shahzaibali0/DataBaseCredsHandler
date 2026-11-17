using DbConnectHub.Core.Models;

namespace DbConnectHub.Core.Abstractions;

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

/// <summary>Stores discovered app metadata.</summary>
public interface IInventoryRepository
{
    InventorySnapshot Load();
    void Save(InventorySnapshot inventory);
}

/// <summary>The stores the app keeps its data in, passed around together.</summary>
public sealed record DataStores(
    IConnectionRepository Connections,
    ICredentialStore Credentials,
    ISettingsStore Settings,
    IInventoryRepository Inventory);

/// <summary>
/// A data file exists but cannot be read (broken JSON, locked, no access). The store has copied it to
/// <see cref="BackupPath"/> and refuses to save over it, so the user's data is never silently replaced.
/// </summary>
public sealed class DataFileUnreadableException : Exception
{
    public DataFileUnreadableException(string filePath, string? backupPath, Exception? cause)
        : base(BuildMessage(filePath, backupPath), cause)
    {
        FilePath = filePath;
        BackupPath = backupPath;
    }

    public string FilePath { get; }

    public string? BackupPath { get; }

    private static string BuildMessage(string filePath, string? backupPath) =>
        backupPath is null
            ? $"The data file {filePath} could not be read."
            : $"The data file {filePath} could not be read. A copy was saved as {backupPath}.";
}

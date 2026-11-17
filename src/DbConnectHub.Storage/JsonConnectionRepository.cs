using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;

namespace DbConnectHub.Storage;

/// <summary>
/// Stores connection metadata as JSON (no passwords) in <c>%APPDATA%\DbConnectHub\connections.json</c>.
/// A file that exists but can't be read throws <see cref="DataFileUnreadableException"/> and is never saved over.
/// </summary>
public sealed class JsonConnectionRepository : IConnectionRepository
{
    private readonly JsonDataFile<List<SavedConnection>> _file;

    public JsonConnectionRepository(string? path = null) =>
        _file = new JsonDataFile<List<SavedConnection>>(path ?? Paths.ConnectionsFile);

    public IReadOnlyList<SavedConnection> GetAll() => _file.Read() ?? [];

    public void SaveAll(IEnumerable<SavedConnection> connections) => _file.Write(connections.ToList());
}

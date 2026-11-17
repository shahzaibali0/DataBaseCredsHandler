using System.Text.Json;
using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Core.Models;

namespace PgCredsHandler.Storage;

/// <summary>
/// Stores connection metadata as JSON (no passwords). Writes atomically via a temp file + replace
/// so a crash mid-write can't corrupt the list.
/// </summary>
public sealed class JsonConnectionRepository : IConnectionRepository
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonConnectionRepository(string? path = null) => _path = path ?? Paths.ConnectionsFile;

    public IReadOnlyList<SavedConnection> GetAll()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<SavedConnection>();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<SavedConnection>>(json, Options) ?? new();
        }
        catch { return Array.Empty<SavedConnection>(); }
    }

    public void SaveAll(IEnumerable<SavedConnection> connections)
    {
        var json = JsonSerializer.Serialize(connections.ToList(), Options);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path)) File.Replace(tmp, _path, null);
        else File.Move(tmp, _path);
    }
}

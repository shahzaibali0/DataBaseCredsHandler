using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;

namespace DbConnectHub.Storage;

/// <summary>
/// Stores discovered app metadata in <c>%APPDATA%\DbConnectHub\inventory.json</c>.
/// Missing file = empty; broken file = backed up, never overwritten.
/// </summary>
public sealed class JsonInventoryRepository : IInventoryRepository
{
    private readonly JsonDataFile<InventorySnapshot> _file;

    public JsonInventoryRepository(string? path = null) =>
        _file = new JsonDataFile<InventorySnapshot>(path ?? Paths.InventoryFile);

    public InventorySnapshot Load() => _file.Read() ?? new InventorySnapshot();

    public void Save(InventorySnapshot inventory) => _file.Write(inventory);
}

using System.Text.Json;
using PgCredsHandler.Core.Abstractions;

namespace PgCredsHandler.Storage;

/// <summary>
/// One JSON file the app owns. "Missing" and "broken" are different: a missing file means start empty;
/// a broken one is backed up, reported, and never saved over, so a bad read can't wipe the user's data.
/// Writes are atomic (temp file + replace) so a crash mid-write can't corrupt the file.
/// </summary>
internal sealed class JsonDataFile<T> where T : class
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly string _path;
    private DataFileUnreadableException? _readFailure;

    public JsonDataFile(string path) => _path = path;

    /// <summary>The stored value, or null when the file does not exist yet.</summary>
    public T? Read()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            _readFailure = null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Options);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _readFailure = new DataFileUnreadableException(_path, TryBackup(), exception);
            throw _readFailure;
        }
    }

    public void Write(T value)
    {
        if (_readFailure is not null) throw _readFailure;

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, Options));
        if (File.Exists(_path)) File.Replace(temporaryPath, _path, null);
        else File.Move(temporaryPath, _path);
    }

    private string? TryBackup()
    {
        var backupPath = $"{_path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        try
        {
            File.Copy(_path, backupPath, overwrite: true);
            return backupPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null; // the original is still untouched; the error message says no copy was made
        }
    }
}

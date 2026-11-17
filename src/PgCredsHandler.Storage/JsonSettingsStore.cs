using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Core.Models;

namespace PgCredsHandler.Storage;

/// <summary>
/// Stores <see cref="AppSettings"/> as JSON in <c>%APPDATA%\PgCredsHandler\settings.json</c>.
/// A file that exists but can't be read throws <see cref="DataFileUnreadableException"/> rather than
/// resetting the settings, because a reset would forget the hidden fingerprints and resurrect deleted entries.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly JsonDataFile<AppSettings> _file;

    public JsonSettingsStore(string? path = null) =>
        _file = new JsonDataFile<AppSettings>(path ?? Paths.SettingsFile);

    public AppSettings Load() => _file.Read() ?? new AppSettings();

    public void Save(AppSettings settings) => _file.Write(settings);
}

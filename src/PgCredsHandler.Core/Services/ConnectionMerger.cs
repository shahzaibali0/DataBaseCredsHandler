using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Util;

namespace PgCredsHandler.Core.Services;

public sealed class MergeResult
{
    public required List<SavedConnection> Connections { get; init; }

    /// <summary>Passwords the caller must write to the credential store (id, username, password).</summary>
    public List<(string Id, string Username, string Password)> PasswordWrites { get; } = new();

    public int Added { get; set; }
    public int Updated { get; set; }
    public int PasswordsChanged { get; set; }
    public int MarkedStale { get; set; }
}

/// <summary>
/// Merges the result of a scan into the saved list. Pure logic: no I/O,
/// the credential store is only read through <c>readPassword</c>.
/// </summary>
public static class ConnectionMerger
{
    public static MergeResult Merge(
        IReadOnlyList<SavedConnection> existing,
        IReadOnlyList<DiscoveredConnection> found,
        IReadOnlyCollection<string> scannedRoots,
        IReadOnlyCollection<string> hiddenFingerprints,
        Func<string, string?> readPassword,
        DateTimeOffset now)
    {
        var result = new MergeResult { Connections = existing.Select(c => c.Clone()).ToList() };
        var hidden = new HashSet<string>(hiddenFingerprints, StringComparer.Ordinal);

        // Index existing entries by fingerprint (manual ones too, so a scan never duplicates them).
        var byFingerprint = new Dictionary<string, SavedConnection>(StringComparer.Ordinal);
        foreach (var c in result.Connections)
            byFingerprint.TryAdd(c.Fingerprint, c);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var d in Deduplicate(found))
        {
            var fp = d.Fingerprint;
            if (hidden.Contains(fp)) continue;
            seen.Add(fp);

            if (byFingerprint.TryGetValue(fp, out var saved))
            {
                if (saved.IsManual) continue; // user owns it now

                saved.ProjectName = d.ProjectName;
                saved.SourceFile = d.SourceFile;
                saved.SourceKind = d.SourceKind;
                saved.SourceKey = d.SourceKey;
                saved.LastSeenAt = now;
                saved.IsStale = false;
                result.Updated++;

                if (d.Password is not null)
                {
                    var current = saved.HasPassword ? readPassword(saved.Id) : null;
                    if (current != d.Password)
                    {
                        if (current is not null)
                        {
                            saved.PasswordChangedAt = now;
                            result.PasswordsChanged++;
                        }
                        saved.HasPassword = true;
                        result.PasswordWrites.Add((saved.Id, saved.Username, d.Password));
                    }
                }
            }
            else
            {
                var c = new SavedConnection
                {
                    ProjectName = d.ProjectName,
                    ProjectPath = d.ProjectPath,
                    SourceFile = d.SourceFile,
                    SourceKind = d.SourceKind,
                    SourceKey = d.SourceKey,
                    Host = d.Host,
                    Port = d.Port,
                    Database = d.Database,
                    Username = d.Username,
                    HasPassword = d.Password is not null,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                };
                result.Connections.Add(c);
                byFingerprint[fp] = c;
                if (d.Password is not null) result.PasswordWrites.Add((c.Id, c.Username, d.Password));
                result.Added++;
            }
        }

        // Anything previously found under a scanned root but not found now -> stale.
        foreach (var c in result.Connections)
        {
            if (c.IsManual || c.IsStale) continue;
            if (seen.Contains(c.Fingerprint)) continue;
            if (scannedRoots.Any(r => ConnectionKey.IsUnder(c.ProjectPath, r)))
            {
                c.IsStale = true;
                result.MarkedStale++;
            }
        }

        return result;
    }

    /// <summary>
    /// The same connection often appears in several files (.env, .env.example, docker-compose).
    /// First, a project with a real config file ignores its example files: they describe another
    /// environment or a template, not a database on this PC. Then keep one per fingerprint:
    /// prefer one with a password, then a non-example file.
    /// </summary>
    public static IEnumerable<DiscoveredConnection> Deduplicate(IEnumerable<DiscoveredConnection> found) =>
        PreferRealConfigFiles(found)
            .GroupBy(d => d.Fingerprint, StringComparer.Ordinal)
            .Select(g => g
                .OrderByDescending(d => d.Password is not null)
                .ThenBy(d => d.IsExampleFile)
                .ThenBy(d => SourcePriority(d.SourceKind))
                .First());

    private static IEnumerable<DiscoveredConnection> PreferRealConfigFiles(IEnumerable<DiscoveredConnection> found) =>
        found
            .GroupBy(d => ConnectionKey.NormalizePath(d.ProjectPath), StringComparer.Ordinal)
            .SelectMany(project => project.Any(d => !d.IsExampleFile) ? project.Where(d => !d.IsExampleFile) : project);

    private static int SourcePriority(string kind) => kind switch
    {
        ".env" => 0,
        "appsettings.json" => 1,
        "docker-compose" => 2,
        _ => 3
    };
}

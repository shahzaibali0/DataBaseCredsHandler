using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Util;
using PgCredsHandler.Scanner.Parsers;

namespace PgCredsHandler.Scanner;

public sealed record ScanProgress(string CurrentFile, int FilesScanned, int ConnectionsFound);

/// <summary>
/// Runs the parsers over every scan root and produces <see cref="DiscoveredConnection"/>s.
/// Fills in defaults (localhost / 5432 / postgres) for parts a parser could not determine.
/// </summary>
public sealed class ProjectScanner
{
    private readonly IReadOnlyList<IConfigParser> _parsers;
    private readonly ProjectWalker _walker;

    public ProjectScanner(AppSettings settings, IReadOnlyList<IConfigParser>? parsers = null)
    {
        _parsers = parsers ?? DefaultParsers.All;
        _walker = new ProjectWalker(settings.ExcludedFolderNames);
    }

    public List<DiscoveredConnection> Scan(
        IEnumerable<string> roots,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<DiscoveredConnection>();
        var filesScanned = 0;
        var npgsqlCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        bool IsConfigFile(string name) => _parsers.Any(p => p.CanParse(name));

        foreach (var root in roots.Distinct())
        {
            foreach (var file in _walker.Walk(root, IsConfigFile, ct))
            {
                ct.ThrowIfCancellationRequested();
                filesScanned++;
                progress?.Report(new ScanProgress(file.FullPath, filesScanned, results.Count));

                var parser = _parsers.FirstOrDefault(p => p.CanParse(file.FileName));
                if (parser is null) continue;

                var context = new ParseContext
                {
                    FilePath = file.FullPath,
                    ProjectRoot = file.ProjectRoot,
                    ProjectUsesNpgsql = () => UsesNpgsql(file.ProjectRoot, npgsqlCache)
                };

                IReadOnlyList<ParsedCredential> parsed;
                try { parsed = parser.Parse(context).ToList(); }
                catch { continue; }

                var isExample = parser is EnvFileParser && EnvFileParser.IsExampleFile(file.FileName);

                foreach (var cred in parsed)
                {
                    var p = cred.Parts;
                    // Skip a hit with nothing but a default-looking host and no user/password/db signal.
                    if (p.Password is null && p.Username is null && p.Database is null && p.Host is null) continue;

                    results.Add(new DiscoveredConnection
                    {
                        ProjectName = Path.GetFileName(file.ProjectRoot.TrimEnd('/', '\\')),
                        ProjectPath = file.ProjectRoot,
                        SourceFile = file.FullPath,
                        SourceKind = parser.Kind,
                        SourceKey = cred.SourceKey,
                        IsExampleFile = isExample,
                        Host = string.IsNullOrWhiteSpace(p.Host) ? "localhost" : p.Host!,
                        Port = p.Port ?? 5432,
                        Database = string.IsNullOrWhiteSpace(p.Database) ? (p.Username ?? "postgres") : p.Database!,
                        Username = string.IsNullOrWhiteSpace(p.Username) ? "postgres" : p.Username!,
                        Password = p.Password,
                    });
                }
            }
        }

        return results;
    }

    private static bool UsesNpgsql(string projectRoot, Dictionary<string, bool> cache)
    {
        if (cache.TryGetValue(projectRoot, out var cached)) return cached;
        var result = false;
        try
        {
            foreach (var csproj in Directory.EnumerateFiles(projectRoot, "*.csproj", SearchOption.AllDirectories).Take(20))
            {
                if (File.ReadAllText(csproj).Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    break;
                }
            }
        }
        catch { /* ignore */ }
        cache[projectRoot] = result;
        return result;
    }
}

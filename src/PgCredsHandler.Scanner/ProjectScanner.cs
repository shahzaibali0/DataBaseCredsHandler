using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Util;
using PgCredsHandler.Scanner.Parsers;

namespace PgCredsHandler.Scanner;

public sealed record ScanProgress(string CurrentFile, int FilesScanned, int ConnectionsFound);

/// <summary>Everything one scan found. Database passwords are in memory only.</summary>
public sealed record ScanFindings(
    IReadOnlyList<DiscoveredConnection> Connections,
    IReadOnlyList<DiscoveredApp> Apps);

/// <summary>
/// Walks every scan root once and collects databases (through the config parsers) and apps (package.json and
/// web .csproj files).
/// Fills in defaults (localhost / 5432 / postgres) for database parts a parser could not determine.
/// </summary>
public sealed class ProjectScanner
{
    private readonly IReadOnlyList<IConfigParser> _parsers;
    private readonly ProjectWalker _walker;
    private readonly WorkspaceLocator _workspaces = new();
    private readonly Dictionary<string, bool> _npgsqlByProject = new(StringComparer.OrdinalIgnoreCase);

    public ProjectScanner(AppSettings settings, IReadOnlyList<IConfigParser>? parsers = null)
    {
        _parsers = parsers ?? DefaultParsers.All;
        _walker = new ProjectWalker(settings.ExcludedFolderNames);
    }

    public ScanFindings Scan(IEnumerable<string> roots, IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        var connections = new List<DiscoveredConnection>();
        var apps = new List<DiscoveredApp>();
        var filesScanned = 0;

        foreach (var root in roots.Distinct())
        {
            foreach (var file in _walker.Walk(root, IsInterestingFile, ct))
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new ScanProgress(file.FullPath, ++filesScanned, connections.Count));

                connections.AddRange(ReadConnections(file));
                if (ReadApp(file, root) is { } app) apps.Add(app);
            }
        }

        return new ScanFindings(connections, apps);
    }

    private bool IsInterestingFile(string name) => AppDetector.IsAppMarker(name) || _parsers.Any(p => p.CanParse(name));

    private IEnumerable<DiscoveredConnection> ReadConnections(FoundFile file)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(file.FileName));
        if (parser is null) return [];

        var context = new ParseContext
        {
            FilePath = file.FullPath,
            ProjectRoot = file.ProjectRoot,
            ProjectUsesNpgsql = () => UsesNpgsql(file.ProjectRoot),
        };

        IReadOnlyList<ParsedCredential> parsed;
        try { parsed = parser.Parse(context).ToList(); }
        catch { return []; } // one malformed file never stops a scan

        var isExample = parser is EnvFileParser && EnvFileParser.IsExampleFile(file.FileName);
        return parsed
            .Select(credential => ToDiscovered(credential, file, parser.Kind, isExample))
            .OfType<DiscoveredConnection>()
            .ToList();
    }

    private DiscoveredApp? ReadApp(FoundFile file, string scanRoot)
    {
        if (!AppDetector.IsAppMarker(file.FileName)) return null;
        var facts = AppDetector.Detect(file.FullPath);
        if (facts is null) return null;

        var appPath = Path.GetDirectoryName(file.FullPath)!;
        var appName = WorkspaceLocator.NameOf(appPath);
        var workspacePath = _workspaces.FindWorkspacePath(appPath, scanRoot);
        return new DiscoveredApp(
            WorkspaceLocator.NameOf(workspacePath), workspacePath, appName, appPath,
            AppDetector.ChooseRole(appName, facts.RoleFromFramework), facts.Framework, facts.LocalUrl, facts.LocalUrlSource)
        {
            Stack = facts.Stack,
            Packages = facts.Packages,
        };
    }

    /// <summary>
    /// Fills in defaults (localhost / 5432 / postgres) for missing parts. Returns null for a hit with no
    /// signal at all, and for template values from example files (<c>USER@HOST:PORT/DATABASE</c>).
    /// </summary>
    private static DiscoveredConnection? ToDiscovered(ParsedCredential credential, FoundFile file, string kind, bool isExample)
    {
        var parts = credential.Parts;
        if (parts.Password is null && parts.Username is null && parts.Database is null && parts.Host is null) return null;

        var host = string.IsNullOrWhiteSpace(parts.Host) ? "localhost" : parts.Host;
        var username = string.IsNullOrWhiteSpace(parts.Username) ? "postgres" : parts.Username;
        var database = string.IsNullOrWhiteSpace(parts.Database) ? (parts.Username ?? "postgres") : parts.Database;
        if (PlaceholderValues.LooksLikePlaceholder(host, database, username)) return null;

        return new DiscoveredConnection
        {
            ProjectName = WorkspaceLocator.NameOf(file.ProjectRoot),
            ProjectPath = file.ProjectRoot,
            SourceFile = file.FullPath,
            SourceKind = kind,
            SourceKey = credential.SourceKey,
            IsExampleFile = isExample,
            Host = host,
            Port = parts.Port ?? 5432,
            Database = database,
            Username = username,
            Password = parts.Password,
        };
    }

    private bool UsesNpgsql(string projectRoot)
    {
        if (_npgsqlByProject.TryGetValue(projectRoot, out var cached)) return cached;
        var usesNpgsql = false;
        try
        {
            usesNpgsql = Directory.EnumerateFiles(projectRoot, "*.csproj", SearchOption.AllDirectories)
                .Take(20)
                .Any(csproj => File.ReadAllText(csproj).Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // can't look inside the project: treat it as not using Npgsql
        }
        _npgsqlByProject[projectRoot] = usesNpgsql;
        return usesNpgsql;
    }
}

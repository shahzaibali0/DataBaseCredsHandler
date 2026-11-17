using DbConnectHub.Core.Models;
using DbConnectHub.Core.Util;
using DbConnectHub.Scanner.Parsers;

namespace DbConnectHub.Scanner;

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
public sealed partial class ProjectScanner
{
    private readonly IReadOnlyList<IConfigParser> _parsers;
    private readonly ProjectWalker _walker;
    private readonly WorkspaceLocator _workspaces = new();
    private readonly Dictionary<string, bool> _npgsqlByProject = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _userSecretsRoot;

    public ProjectScanner(AppSettings settings, IReadOnlyList<IConfigParser>? parsers = null, string? userSecretsRoot = null)
    {
        _parsers = parsers ?? DefaultParsers.All;
        _userSecretsRoot = userSecretsRoot ?? DefaultUserSecretsRoot();
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
                connections.AddRange(ReadUserSecrets(file));
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

    /// <summary>Where .NET keeps User Secrets: %APPDATA%\Microsoft\UserSecrets.</summary>
    public static string DefaultUserSecretsRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets");

    /// <summary>
    /// A .NET project often keeps its dev connection string in User Secrets, outside the project folder, so it never
    /// reaches git. Only the secrets file named by this project's UserSecretsId is opened, and only database
    /// connection strings are taken from it (the same rules as appsettings.json); every other value is ignored.
    /// </summary>
    private IEnumerable<DiscoveredConnection> ReadUserSecrets(FoundFile file)
    {
        if (!file.FileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return [];

        string project;
        try { project = File.ReadAllText(file.FullPath); }
        catch (IOException) { return []; } // unreadable project file: nothing to follow

        var secretsId = UserSecretsIdRegex().Match(project);
        if (!secretsId.Success || secretsId.Groups[1].Value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return [];

        var secretsFile = Path.Combine(_userSecretsRoot, secretsId.Groups[1].Value, "secrets.json");
        if (!File.Exists(secretsFile)) return [];

        var projectFolder = Path.GetDirectoryName(file.FullPath)!;
        var context = new ParseContext
        {
            FilePath = secretsFile,
            ProjectRoot = projectFolder,
            ProjectUsesNpgsql = () => project.Contains("Npgsql", StringComparison.OrdinalIgnoreCase),
        };
        var secretsAsFound = new FoundFile(secretsFile, "secrets.json", projectFolder);
        return new AppSettingsJsonParser().Parse(context)
            .Select(credential => ToDiscovered(credential, secretsAsFound, "user secrets", isExample: false))
            .OfType<DiscoveredConnection>()
            .ToList();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<UserSecretsId>\s*([^<\s]+)\s*</UserSecretsId>")]
    private static partial System.Text.RegularExpressions.Regex UserSecretsIdRegex();

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
            Password = PlaceholderValues.IsTemplatePassword(parts.Password) ? null : parts.Password,
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

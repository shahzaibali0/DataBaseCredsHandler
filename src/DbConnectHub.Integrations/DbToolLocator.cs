namespace DbConnectHub.Integrations;

/// <summary>
/// Finds installed database clients in their usual install folders: per-user (AppData\Local, AppData\Local\Programs)
/// and machine-wide (Program Files, Program Files (x86)).
/// </summary>
public sealed class DbToolLocator
{
    private readonly IReadOnlyList<string> _installRoots;

    /// <param name="installRoots">Folders that hold programs; <see cref="DefaultInstallRoots"/> on a real PC.</param>
    public DbToolLocator(IReadOnlyList<string> installRoots) => _installRoots = installRoots;

    public static IReadOnlyList<string> DefaultInstallRoots() =>
        new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        }
        .Where(folder => !string.IsNullOrEmpty(folder))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>The client's program file, or null when it isn't installed.</summary>
    public string? Find(DbTool tool) =>
        _installRoots.SelectMany(root => Candidates(root, tool)).FirstOrDefault(File.Exists);

    private static IEnumerable<string> Candidates(string root, DbTool tool) => tool switch
    {
        DbTool.DBeaver => [Path.Combine(root, "DBeaver", "dbeaver.exe")],
        DbTool.DbVisualizer => [Path.Combine(root, "DbVisualizer", "dbvis.exe")],
        _ => PgAdminCandidates(root),
    };

    // pgAdmin 4 installs on its own to "pgAdmin 4\runtime" (v7 and later) or "pgAdmin 4\v6\runtime" (older),
    // and the PostgreSQL installer bundles it as "PostgreSQL\<version>\pgAdmin 4\runtime".
    private static IEnumerable<string> PgAdminCandidates(string root) =>
        new[] { Path.Combine(root, "pgAdmin 4") }
            .Concat(SubfoldersNewestFirst(Path.Combine(root, "PostgreSQL"), "*").Select(version => Path.Combine(version, "pgAdmin 4")))
            .SelectMany(installFolder => new[] { Path.Combine(installFolder, "runtime", "pgAdmin4.exe") }
                .Concat(SubfoldersNewestFirst(installFolder, "v*").Select(version => Path.Combine(version, "runtime", "pgAdmin4.exe"))));

    private static IEnumerable<string> SubfoldersNewestFirst(string folder, string pattern)
    {
        if (!Directory.Exists(folder)) return [];
        try
        {
            return Directory.EnumerateDirectories(folder, pattern)
                .OrderByDescending(path => int.TryParse(Path.GetFileName(path).TrimStart('v'), out var version) ? version : 0)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return []; // a folder we can't list simply has no candidates
        }
    }
}

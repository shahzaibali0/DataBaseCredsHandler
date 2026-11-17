using PgCredsHandler.Core.Models;

namespace PgCredsHandler.Scanner;

public sealed record FoundFile(string FullPath, string FileName, string ProjectRoot);

/// <summary>
/// Walks the scan roots and yields config files worth parsing, skipping excluded folders
/// (node_modules, .git, venv, ...). "Project root" is the nearest ancestor that looks like a
/// project (has a .git, package.json, .csproj, pyproject.toml, go.mod, composer.json, etc.),
/// else the file's own folder.
/// </summary>
public sealed class ProjectWalker
{
    private readonly HashSet<string> _excluded;
    private readonly int _maxDepth;

    private static readonly string[] ProjectMarkers =
    {
        ".git", "package.json", "pyproject.toml", "requirements.txt", "go.mod", "composer.json",
        "Cargo.toml", "pom.xml", "build.gradle", "Gemfile", "manage.py", "artisan"
    };

    public ProjectWalker(IEnumerable<string> excludedFolderNames, int maxDepth = 12)
    {
        _excluded = new HashSet<string>(excludedFolderNames, StringComparer.OrdinalIgnoreCase);
        _maxDepth = maxDepth;
    }

    public IEnumerable<FoundFile> Walk(string root, Func<string, bool> isConfigFile, CancellationToken ct = default)
    {
        if (!Directory.Exists(root)) yield break;

        var stack = new Stack<(string Dir, int Depth)>();
        stack.Push((root, 0));

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (dir, depth) = stack.Pop();

            string[] files;
            string[] subdirs;
            try
            {
                files = Directory.GetFiles(dir);
                subdirs = Directory.GetDirectories(dir);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            var projectRoot = FindProjectRoot(dir, root);

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (isConfigFile(name))
                    yield return new FoundFile(file, name, projectRoot);
            }

            if (depth >= _maxDepth) continue;

            foreach (var sub in subdirs)
            {
                var name = Path.GetFileName(sub);
                if (_excluded.Contains(name)) continue;
                if (name.StartsWith('.') && name is not ".config") continue; // skip dotfolders except a couple
                try
                {
                    if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue; // don't follow junctions/symlinks
                }
                catch { continue; }
                stack.Push((sub, depth + 1));
            }
        }
    }

    private static string FindProjectRoot(string dir, string scanRoot)
    {
        var current = dir;
        while (true)
        {
            try
            {
                if (ProjectMarkers.Any(m => File.Exists(Path.Combine(current, m)) || Directory.Exists(Path.Combine(current, m))))
                    return current;
            }
            catch { /* ignore */ }

            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || current.Equals(scanRoot, StringComparison.OrdinalIgnoreCase) || parent.Length < scanRoot.Length)
                return dir; // no marker found: the file's own folder is the "project"
            current = parent;
        }
    }
}

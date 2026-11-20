using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DbConnectHub.Integrations;

/// <summary>
/// Starts a project's dev server in its own console window, where the user sees its output and closes the window to
/// stop it. It runs through cmd.exe because npm, pnpm and yarn are .cmd scripts that only cmd can start.
/// </summary>
public static partial class ServerLauncher
{
    // Read by Windows' NeedCurrentDirectoryForExePath, which cmd.exe asks before searching the current folder.
    private const string NoCurrentFolderInExePath = "NoDefaultCurrentDirectoryInExePath";

    /// <summary>
    /// The start info for <c>cmd.exe /k program arguments…</c> in <paramref name="folder"/>. Null when a word isn't a
    /// plain word: cmd.exe would read a quote, &amp;, | or % as its own syntax, so nothing else ever reaches it.
    /// </summary>
    public static ProcessStartInfo? CreateStartInfo(string program, IReadOnlyList<string> arguments, string folder)
    {
        if (!PlainWordRegex().IsMatch(program) || !arguments.All(PlainWordRegex().IsMatch)) return null;

        // The full path to cmd.exe, so a cmd.exe sitting in the project folder is never the one started.
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = folder,
        };
        // /k keeps the window open after the server stops, so an error it printed stays readable.
        foreach (var word in new[] { "/k", program }.Concat(arguments)) startInfo.ArgumentList.Add(word);
        // Set by VS Code for everything started from it; an Electron dev server that inherits it runs as plain Node.
        startInfo.Environment.Remove("ELECTRON_RUN_AS_NODE");
        // cmd looks for "npm" in the current folder before PATH; this makes it use PATH only, so an npm.cmd dropped in
        // the project isn't the one run. (The dev script itself is the project's own code, which Run runs on purpose.)
        startInfo.Environment[NoCurrentFolderInExePath] = "1";
        return startInfo;
    }

    public static LaunchResult Start(string program, IReadOnlyList<string> arguments, string folder)
    {
        var startInfo = CreateStartInfo(program, arguments, folder);
        if (startInfo is null) return new LaunchResult(false, "That start command can't be run safely.");
        if (!Directory.Exists(folder)) return new LaunchResult(false, $"The folder {folder} doesn't exist any more. Rescan to update the list.");

        // Disposing the Process object only lets go of it; the server keeps running in its window.
        using var process = Process.Start(startInfo);
        return process is null
            ? new LaunchResult(false, "Windows didn't start the console window.")
            : new LaunchResult(true, $"Started \"{string.Join(' ', [program, .. arguments])}\" in a new window. Close that window to stop the server.");
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:-]*$")]
    private static partial Regex PlainWordRegex();
}

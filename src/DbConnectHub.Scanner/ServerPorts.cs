using System.Text;
using System.Text.RegularExpressions;
using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;

namespace DbConnectHub.Scanner;

/// <summary>Whether a port change can be (or was) made, and what it changes or why not, in words.</summary>
public sealed record PortChange(bool IsPossible, string Message);

/// <summary>
/// Changes the port an app's dev server runs on, in the one place the scan read it from: the port in the dev script,
/// the PORT line of an .env file, or the URL in launchSettings.json. Only that value is replaced; every other byte of
/// the file stays as it was. A framework default gets "--port N" added to a plain dev script. This is the only code
/// that writes to the user's projects, and it runs only when the user confirms a change.
/// </summary>
public sealed partial class ServerPorts
{
    public const int LowestPort = 1024;
    public const int HighestPort = 65535;

    // Frameworks whose own command line takes --port, so a default port can be changed by adding it to the script.
    private static readonly HashSet<string> FrameworksWithPortOption =
        new(["Next.js", "Nuxt", "Angular", "SvelteKit", "Vite"], StringComparer.Ordinal);

    private const string TempMarker = ".dbconnecthub-tmp";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly IInventoryRepository _inventory;

    public ServerPorts(IInventoryRepository inventory) => _inventory = inventory;

    /// <summary>What changing the port would do, without changing anything.</summary>
    public static PortChange Preview(SavedApp app, int newPort)
    {
        var edit = PlanEdit(app, newPort);
        return new PortChange(edit.BuildText is not null, edit.Message);
    }

    /// <summary>Changes the port in the app's file, then stores the app's new address.</summary>
    public PortChange Change(SavedApp app, int newPort)
    {
        var edit = PlanEdit(app, newPort);
        if (edit.FilePath is null || edit.BuildText is null) return new PortChange(false, edit.Message);

        var writeProblem = WriteAtomically(edit.FilePath, edit.BuildText(), edit.HasBom);
        if (writeProblem is not null) return new PortChange(false, writeProblem);
        const string Restart = "Restart the server for it to take effect.";
        return new PortChange(true, TrySaveDetectedAddress(app)
            ? $"Done. {edit.Message} {Restart}"
            : $"Done. {edit.Message} {Restart} The address shown here updates on the next scan.");
    }

    // The edited file is only built when it is written: an .env file holds secrets, and the preview runs on every
    // keystroke, so the preview never makes a copy. Neither the text nor the builder ever leaves this class.
    private sealed record FileEdit(string? FilePath, Func<string>? BuildText, bool HasBom, string Message);

    private sealed record TextFile(string Path, string Text, bool HasBom);

    private static FileEdit Refuse(string message) => new(null, null, false, message);

    private static FileEdit PlanEdit(SavedApp app, int newPort)
    {
        if (newPort is < LowestPort or > HighestPort) return Refuse($"Choose a port from {LowestPort} to {HighestPort}.");
        if (CurrentPort(app) == newPort) return Refuse($"The server already runs on {newPort}.");
        return app.Framework == AppDetector.AspNetCore ? PlanLaunchSettingsEdit(app, newPort) : PlanNodeEdit(app, newPort);
    }

    private static int? CurrentPort(SavedApp app) =>
        Uri.TryCreate(app.LocalUrl, UriKind.Absolute, out var url) ? url.Port : null;

    // Same order as AppDetector reads it: the dev script's port, then an .env PORT, then the framework's default.
    private static FileEdit PlanNodeEdit(SavedApp app, int newPort)
    {
        var packageProblem = TryReadText(Path.Combine(app.AppPath, "package.json"), out var package);
        if (packageProblem is not null) return Refuse(packageProblem);
        using var document = AppDetector.TryReadPackageJson(package.Path);
        var scriptName = document is null ? null : AppDetector.ServerScriptName(document.RootElement);
        if (scriptName is null) return Refuse("package.json has no dev or start script to change.");

        var script = ScriptValue(package.Text, scriptName);
        if (script is null) return Refuse($"package.json has more than one \"{scriptName}\" entry; change the port by hand.");

        var portInScript = AppDetector.PortInScriptRegex().Match(script.Value);
        if (portInScript.Success)
        {
            var portDigits = portInScript.Groups[1];
            return EditOf(package, () => ReplaceGroup(package.Text, script, ReplaceGroup(script.Value, portDigits, newPort.ToString())),
                $"Changes {portInScript.Value} to {newPort} in the \"{scriptName}\" script of package.json.");
        }

        var envEdit = PlanEnvEdit(app.AppPath, newPort);
        if (envEdit is not null) return envEdit;

        if (!FrameworksWithPortOption.Contains(app.Framework))
            return Refuse($"This {app.Framework} server takes its port from its own code, so change it there.");
        if (!PlainCommandRegex().IsMatch(script.Value))
            return Refuse($"The \"{scriptName}\" script runs more than one command; add --port {newPort} to it by hand.");
        return EditOf(package, () => ReplaceGroup(package.Text, script, $"{script.Value} --port {newPort}"),
            $"Adds --port {newPort} to the \"{scriptName}\" script of package.json.");
    }

    // Only the digits of the first PORT line are replaced, so every other line, secrets included, is kept byte for byte.
    private static FileEdit? PlanEnvEdit(string appFolder, int newPort)
    {
        foreach (var fileName in AppDetector.EnvFilesForPort)
        {
            var path = Path.Combine(appFolder, fileName);
            if (!File.Exists(path)) continue;
            var envProblem = TryReadText(path, out var envFile);
            if (envProblem is not null) return Refuse(envProblem);
            // The scan also ends a line at a lone \r; PortLineRegex doesn't, so it could miss the line the scan read.
            if (LoneCarriageReturnRegex().IsMatch(envFile.Text))
                return Refuse($"{fileName} uses old Mac line endings; change its PORT line by hand.");

            var portLine = PortLineRegex().Match(envFile.Text);
            if (!portLine.Success) continue;

            var portDigits = portLine.Groups[1];
            return EditOf(envFile, () => ReplaceGroup(envFile.Text, portDigits, newPort.ToString()),
                $"Changes PORT={portDigits.Value} to PORT={newPort} in {fileName}.");
        }
        return null;
    }

    private static FileEdit PlanLaunchSettingsEdit(SavedApp app, int newPort)
    {
        var path = Path.Combine(app.AppPath, "Properties", "launchSettings.json");
        var oldUrl = File.Exists(path) ? AppDetector.ReadLaunchUrl(path) : null;
        if (oldUrl is null)
            return Refuse("This server uses ASP.NET Core's default port; add a Properties/launchSettings.json to set one.");

        var settingsProblem = TryReadText(path, out var settings);
        if (settingsProblem is not null) return Refuse(settingsProblem);
        var authority = AuthorityRegex().Match(oldUrl);
        if (!authority.Success) return Refuse($"{oldUrl} has no port number to change; add one in launchSettings.json by hand.");

        // Only "scheme://host:port" is replaced, so a path after it (/swagger) stays. Every profile on this address
        // moves with it; "(?!\d)" keeps :5001 from matching inside :50010.
        var oldAuthority = authority.Value;
        var newAuthority = $"{authority.Groups[1].Value}:{newPort}";
        return EditOf(settings, () => Regex.Replace(settings.Text, Regex.Escape(oldAuthority) + @"(?!\d)", newAuthority),
            $"Changes {oldAuthority} to {newAuthority} in Properties/launchSettings.json.");
    }

    private static FileEdit EditOf(TextFile file, Func<string> buildText, string message) => new(file.Path, buildText, file.HasBom, message);

    // The raw JSON string of one script. Null when the key appears more than once, so the wrong one is never edited.
    private static Group? ScriptValue(string packageJson, string scriptName)
    {
        var matches = Regex.Matches(packageJson, $"\"{Regex.Escape(scriptName)}\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        return matches.Count == 1 ? matches[0].Groups[1] : null;
    }

    private static string ReplaceGroup(string text, Group group, string replacement) =>
        string.Concat(text.AsSpan(0, group.Index), replacement, text.AsSpan(group.Index + group.Length));

    // Null when the file can be edited, otherwise why not. UTF-8 only, refused if any byte isn't valid UTF-8, so writing
    // the text back can't change other characters. A link is refused: replacing it would cut it off from the file it
    // points to (a monorepo's shared .env), and links are never followed.
    private static string? TryReadText(string path, out TextFile file)
    {
        var name = Path.GetFileName(path);
        file = new TextFile(path, "", false);
        try
        {
            if (new FileInfo(path).LinkTarget is not null) return $"{name} is a link to another file; change the port there by hand.";
            var bytes = File.ReadAllBytes(path);
            var hasBom = bytes.AsSpan().StartsWith(Utf8Bom);
            var text = StrictUtf8.GetString(bytes, hasBom ? Utf8Bom.Length : 0, bytes.Length - (hasBom ? Utf8Bom.Length : 0));
            file = new TextFile(path, text, hasBom);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return $"{name} can't be read as UTF-8 text, so it isn't changed.";
        }
    }

    // A temp file next to it, then File.Replace, so a crash halfway never leaves the user's file half written. The temp
    // file is a full copy (of an .env: every secret in it), so it is never left behind without the user being told.
    private static string? WriteAtomically(string path, string text, bool hasBom)
    {
        var tempPath = TempPathFor(path);
        var name = Path.GetFileName(path);
        if (!TryDelete(tempPath)) return $"{Path.GetFileName(tempPath)} is in the way next to {name}; delete it, then try again.";
        try
        {
            File.WriteAllText(tempPath, text, new UTF8Encoding(hasBom));
            File.Replace(tempPath, path, destinationBackupFileName: null);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var leftover = TryDelete(tempPath) ? "" : $" A copy of {name} was left at {tempPath}; delete it.";
            return $"{name} couldn't be changed: {exception.Message}{leftover}";
        }
    }

    // ".env.local" -> ".env.dbconnecthub-tmp.local" and ".env" -> ".env.dbconnecthub-tmp", so the ignore rules that keep
    // the file out of git (*.local, .env*) most likely match the copy too, if a crash ever leaves it behind.
    private static string TempPathFor(string path)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(name);
        var hasInnerExtension = extension.Length > 0 && extension.Length < name.Length && name.IndexOf('.', 1) > 0;
        var tempName = hasInnerExtension ? $"{name[..^extension.Length]}{TempMarker}{extension}" : $"{name}{TempMarker}";
        return Path.Combine(Path.GetDirectoryName(path) ?? "", tempName);
    }

    // True when the file is gone afterwards, or never existed.
    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // The scan's own detector reads the changed file, so the screen shows exactly what the next scan would find. The
    // project file is already changed by now, so a failure here becomes "updates on the next scan", never a crash.
    private bool TrySaveDetectedAddress(SavedApp app)
    {
        try
        {
            var facts = AppDetector.Detect(MarkerFile(app));
            if (facts is null) return false;
            var inventory = _inventory.Load();
            foreach (var saved in inventory.Apps.Where(saved => saved.AppPath == app.AppPath && saved.Framework == app.Framework))
            {
                saved.LocalUrl = facts.LocalUrl;
                saved.LocalUrlSource = facts.LocalUrlSource;
            }
            _inventory.Save(inventory);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string MarkerFile(SavedApp app) =>
        app.Framework == AppDetector.AspNetCore
            ? Directory.EnumerateFiles(app.AppPath, "*.csproj").FirstOrDefault() ?? Path.Combine(app.AppPath, "app.csproj")
            : Path.Combine(app.AppPath, "package.json");

    // The first PORT line, matched as AppDetector matches it, but found in the whole text so only its digits change.
    [GeneratedRegex(@"^[ \t]*(?:export[ \t]+)?PORT[ \t]*=[ \t]*[""']?(\d{2,5})[""']?(?:[ \t]+#[^\r\n]*)?[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PortLineRegex();

    [GeneratedRegex(@"\r(?!\n)")]
    private static partial Regex LoneCarriageReturnRegex();

    // "http://localhost:5001" out of "http://localhost:5001/swagger".
    [GeneratedRegex(@"^(https?://[^/:;\s""]+):\d+")]
    private static partial Regex AuthorityRegex();

    // One CLI call with plain words and flags, e.g. "next dev --turbopack": no &&, pipes, quotes or a second command.
    [GeneratedRegex(@"^\s*(next|nuxt|nuxi|ng|vite)(\s+[\w:.=-]+)*\s*$")]
    private static partial Regex PlainCommandRegex();
}

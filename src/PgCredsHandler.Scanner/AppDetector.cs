using System.Text.Json;
using System.Text.RegularExpressions;
using PgCredsHandler.Core.Models;

namespace PgCredsHandler.Scanner;

/// <summary>What the detector learned from an app's marker file, before the project is known.</summary>
public sealed record AppFacts(string Framework, AppRole RoleFromFramework, string? LocalUrl, string? LocalUrlSource)
{
    /// <summary>Framework, UI library, language and runtime, e.g. "Next.js 16", "React 19", "TypeScript".</summary>
    public IReadOnlyList<string> Stack { get; init; } = [];

    public IReadOnlyList<KeyPackage> Packages { get; init; } = [];
}

/// <summary>
/// Recognises an app from its package.json or .csproj: the framework, and where its dev server runs
/// (from the dev script, a PORT in .env, launchSettings.json, or the framework's default port).
/// </summary>
public static partial class AppDetector
{
    // Checked in order: the first dependency found names the framework.
    private static readonly (string Package, string Framework, AppRole Role, int DefaultPort)[] JsFrameworks =
    [
        ("next", "Next.js", AppRole.App, 3000),
        ("nuxt", "Nuxt", AppRole.Frontend, 3000),
        ("@angular/core", "Angular", AppRole.Frontend, 4200),
        ("@sveltejs/kit", "SvelteKit", AppRole.Frontend, 5173),
        ("vite", "Vite", AppRole.Frontend, 5173),
        ("react-scripts", "Create React App", AppRole.Frontend, 3000),
        ("@nestjs/core", "NestJS", AppRole.Backend, 3000),
        ("fastify", "Fastify", AppRole.Backend, 3000),
        ("express", "Express", AppRole.Backend, 3000),
        ("hono", "Hono", AppRole.Backend, 3000),
    ];

    private static readonly string[] EnvFilesForPort = [".env.local", ".env.development.local", ".env.development", ".env"];

    [GeneratedRegex(@"(?:--port[ =]|-p[ =]?|PORT=)(\d{2,5})\b")]
    private static partial Regex PortInScriptRegex();

    [GeneratedRegex(@"https?://[^;\s""]+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"<TargetFrameworks?>\s*net(\d+)\.(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PackageReferenceRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex MajorVersionRegex();

    [GeneratedRegex(@"^\s*(?:export\s+)?PORT\s*=\s*[""']?(\d{2,5})[""']?(?:\s+#.*)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PortAssignmentRegex();

    public static bool IsAppMarker(string fileName) =>
        fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    /// <summary>The app described by a marker file, or null when it isn't a runnable app (a library, a tool config).</summary>
    public static AppFacts? Detect(string markerFilePath) =>
        markerFilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? DetectDotNet(markerFilePath)
            : DetectNode(markerFilePath);

    /// <summary>Backend or frontend from the folder name first, then from the framework.</summary>
    public static AppRole ChooseRole(string appName, AppRole roleFromFramework)
    {
        var name = appName.ToLowerInvariant();
        if (new[] { "backend", "api", "server", "service" }.Any(name.Contains)) return AppRole.Backend;
        if (new[] { "frontend", "web", "client", "ui", "site", "dashboard", "admin" }.Any(name.Contains)) return AppRole.Frontend;
        return roleFromFramework;
    }

    private static AppFacts? DetectNode(string packageJsonPath)
    {
        using var document = TryReadJson(packageJsonPath);
        if (document is null) return null;

        var root = document.RootElement;
        var framework = JsFrameworks.FirstOrDefault(candidate => HasDependency(root, candidate.Package));
        var devScript = ReadScript(root, "dev") ?? ReadScript(root, "start");
        if (framework.Framework is null && devScript is null) return null; // tooling-only package.json

        var appFolder = Path.GetDirectoryName(packageJsonPath)!;
        var (port, source) = FindNodePort(devScript, appFolder, framework);
        var url = port is null ? null : $"http://localhost:{port}";
        return new AppFacts(framework.Framework ?? "Node.js", framework.Framework is null ? AppRole.App : framework.Role, url, source)
        {
            Stack = NodeStack(root, appFolder, framework.Package, framework.Framework),
            Packages = PackageCatalog.ForNpm(DependencyNames(root)),
        };
    }

    private static (int? Port, string? Source) FindNodePort(
        string? devScript, string appFolder, (string Package, string Framework, AppRole Role, int DefaultPort) framework)
    {
        var scriptMatch = devScript is null ? null : PortInScriptRegex().Match(devScript);
        if (scriptMatch is { Success: true }) return (int.Parse(scriptMatch.Groups[1].Value), "package.json dev script");

        var envPort = FindPortInEnvFiles(appFolder);
        if (envPort is not null) return (envPort, ".env PORT");

        return framework.Framework is null ? (null, null) : (framework.DefaultPort, $"{framework.Framework} default");
    }

    private static int? FindPortInEnvFiles(string appFolder)
    {
        foreach (var fileName in EnvFilesForPort)
        {
            var path = Path.Combine(appFolder, fileName);
            if (!File.Exists(path)) continue;
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    var match = PortAssignmentRegex().Match(line);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var port)) return port;
                }
            }
            catch (IOException) { /* unreadable env file: try the next one */ }
        }
        return null;
    }

    private static AppFacts? DetectDotNet(string csprojPath)
    {
        string project;
        try { project = File.ReadAllText(csprojPath); }
        catch (IOException) { return null; }

        // Only web projects run a local server; libraries, tests and desktop apps don't.
        if (!project.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)) return null;

        var launchSettings = Path.Combine(Path.GetDirectoryName(csprojPath)!, "Properties", "launchSettings.json");
        var url = ReadLaunchUrl(launchSettings);
        var facts = url is null
            ? new AppFacts("ASP.NET Core", AppRole.Backend, "http://localhost:5000", "ASP.NET Core default")
            : new AppFacts("ASP.NET Core", AppRole.Backend, url, "launchSettings.json");
        return facts with
        {
            Stack = DotNetStack(project),
            Packages = PackageCatalog.ForNuGet(PackageReferenceRegex().Matches(project).Select(match => match.Groups[1].Value)),
        };
    }

    private static string? ReadLaunchUrl(string launchSettingsPath)
    {
        if (!File.Exists(launchSettingsPath)) return null;
        string text;
        try { text = File.ReadAllText(launchSettingsPath); }
        catch (IOException) { return null; }

        var urls = UrlRegex().Matches(text).Select(match => match.Value).Where(url => url.Contains("localhost")).ToList();
        return urls.FirstOrDefault(url => url.StartsWith("http://", StringComparison.Ordinal)) ?? urls.FirstOrDefault();
    }

    // "Next.js 16", "React 19", "TypeScript", "Node.js": what someone joining the project needs to know first.
    private static List<string> NodeStack(JsonElement root, string appFolder, string? frameworkPackage, string? framework)
    {
        var stack = new List<string>();
        if (framework is not null) stack.Add(WithMajorVersion(framework, DependencyVersion(root, frameworkPackage!)));
        if (frameworkPackage != "react-scripts" && DependencyVersion(root, "react") is { } react) stack.Add(WithMajorVersion("React", react));
        if (DependencyVersion(root, "vue") is { } vue && framework != "Nuxt") stack.Add(WithMajorVersion("Vue", vue));
        var usesTypeScript = DependencyVersion(root, "typescript") is not null || File.Exists(Path.Combine(appFolder, "tsconfig.json"));
        stack.Add(usesTypeScript ? "TypeScript" : "JavaScript");
        stack.Add("Node.js");
        return stack;
    }

    private static List<string> DotNetStack(string project)
    {
        var target = TargetFrameworkRegex().Match(project);
        var runtime = target.Success ? $".NET {target.Groups[1].Value}" : ".NET";
        return ["ASP.NET Core", "C#", runtime];
    }

    // "^16.0.1" → "Next.js 16"; a version that isn't a number (e.g. "latest") leaves just the name.
    private static string WithMajorVersion(string name, string? version)
    {
        var major = version is null ? null : MajorVersionRegex().Match(version);
        return major is { Success: true } ? $"{name} {major.Value}" : name;
    }

    private static string? DependencyVersion(JsonElement root, string package) =>
        new[] { "dependencies", "devDependencies" }
            .Select(section => root.TryGetProperty(section, out var dependencies) && dependencies.ValueKind == JsonValueKind.Object
                && dependencies.TryGetProperty(package, out var version) && version.ValueKind == JsonValueKind.String
                    ? version.GetString()
                    : null)
            .FirstOrDefault(version => version is not null);

    private static IEnumerable<string> DependencyNames(JsonElement root) =>
        new[] { "dependencies", "devDependencies" }
            .Where(section => root.TryGetProperty(section, out var dependencies) && dependencies.ValueKind == JsonValueKind.Object)
            .SelectMany(section => root.GetProperty(section).EnumerateObject().Select(property => property.Name));

    private static JsonDocument? TryReadJson(string path)
    {
        try { return JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true }); }
        catch (Exception exception) when (exception is JsonException or IOException) { return null; }
    }

    private static bool HasDependency(JsonElement root, string package) =>
        new[] { "dependencies", "devDependencies" }.Any(section =>
            root.TryGetProperty(section, out var dependencies)
            && dependencies.ValueKind == JsonValueKind.Object
            && dependencies.TryGetProperty(package, out _));

    private static string? ReadScript(JsonElement root, string name) =>
        root.TryGetProperty("scripts", out var scripts)
        && scripts.ValueKind == JsonValueKind.Object
        && scripts.TryGetProperty(name, out var script)
        && script.ValueKind == JsonValueKind.String
            ? script.GetString()
            : null;
}

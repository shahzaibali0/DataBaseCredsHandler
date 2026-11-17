using PgCredsHandler.Core.Util;
using YamlDotNet.RepresentationModel;

namespace PgCredsHandler.Scanner.Parsers;

/// <summary>
/// Reads docker-compose.yml. For each service whose image looks like Postgres, it builds a
/// connection from the POSTGRES_USER / POSTGRES_PASSWORD / POSTGRES_DB environment variables
/// and the host-side port from the "ports:" mapping ("5433:5432" -> host port 5433).
/// </summary>
public sealed class DockerComposeParser : IConfigParser
{
    public string Kind => "docker-compose";

    public bool CanParse(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        return n is "docker-compose.yml" or "docker-compose.yaml"
            or "compose.yml" or "compose.yaml"
            || (n.StartsWith("docker-compose.") && (n.EndsWith(".yml") || n.EndsWith(".yaml")));
    }

    public IEnumerable<ParsedCredential> Parse(ParseContext context)
    {
        var results = new List<ParsedCredential>();

        YamlMappingNode? root;
        try
        {
            using var reader = new StreamReader(context.FilePath);
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count == 0) return results;
            root = yaml.Documents[0].RootNode as YamlMappingNode;
        }
        catch
        {
            return results; // malformed compose file: skip quietly
        }

        if (root is null) return results;

        var services = Get(root, "services") as YamlMappingNode;
        var topLevelEnv = ReadDotEnvBesideCompose(context.FilePath);
        if (services is null) return results;

        foreach (var (nameNode, serviceNode) in services.Children)
        {
            if (serviceNode is not YamlMappingNode service) continue;
            var serviceName = (nameNode as YamlScalarNode)?.Value ?? "db";

            var image = (Get(service, "image") as YamlScalarNode)?.Value ?? "";
            var env = ReadEnvironment(service, topLevelEnv);

            var isPostgres = LooksLikePostgresImage(image)
                             || env.Keys.Any(k => k.StartsWith("POSTGRES", StringComparison.OrdinalIgnoreCase))
                             || env.ContainsKey("PGDATA");
            if (!isPostgres) continue;

            var user = env.GetValueOrDefault("POSTGRES_USER") ?? env.GetValueOrDefault("PGUSER") ?? "postgres";
            var password = env.GetValueOrDefault("POSTGRES_PASSWORD") ?? env.GetValueOrDefault("PGPASSWORD");
            var db = env.GetValueOrDefault("POSTGRES_DB") ?? env.GetValueOrDefault("PGDATABASE") ?? user;
            var hostPort = ReadHostPort(service) ?? 5432;

            // From your PC the container is reachable on localhost at the published port.
            var parts = new PgParts("localhost", hostPort, db, user, password);
            results.Add(new ParsedCredential($"service: {serviceName}", parts));
        }

        return results;
    }

    private static Dictionary<string, string> ReadEnvironment(YamlMappingNode service, IReadOnlyDictionary<string, string> interpolation)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        var node = Get(service, "environment");

        void Add(string key, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            env[key] = VariableExpander.Expand(value, interpolation);
        }

        switch (node)
        {
            case YamlMappingNode map:
                foreach (var (k, v) in map.Children)
                    Add(((YamlScalarNode)k).Value ?? "", (v as YamlScalarNode)?.Value);
                break;
            case YamlSequenceNode seq:
                foreach (var item in seq.Children)
                {
                    var s = (item as YamlScalarNode)?.Value;
                    if (s is null) continue;
                    var eq = s.IndexOf('=');
                    if (eq > 0) Add(s[..eq].Trim(), s[(eq + 1)..]);
                    else if (interpolation.TryGetValue(s.Trim(), out var passthrough)) Add(s.Trim(), passthrough);
                }
                break;
        }

        // env_file references are not followed here beyond the sibling .env; keep it simple.
        return env;
    }

    private static int? ReadHostPort(YamlMappingNode service)
    {
        if (Get(service, "ports") is not YamlSequenceNode ports) return null;

        foreach (var entry in ports.Children)
        {
            // Long form: { published: 5433, target: 5432 }
            if (entry is YamlMappingNode m)
            {
                var target = (Get(m, "target") as YamlScalarNode)?.Value;
                var published = (Get(m, "published") as YamlScalarNode)?.Value;
                if (target == "5432" && int.TryParse(published, out var lp)) return lp;
                continue;
            }

            // Short form: "5433:5432", "127.0.0.1:5433:5432", "5432"
            var s = (entry as YamlScalarNode)?.Value;
            if (s is null) continue;
            var segs = s.Split(':');
            var container = segs[^1];
            if (!container.StartsWith("5432")) continue;
            if (segs.Length >= 2 && int.TryParse(segs[^2], out var hostPort)) return hostPort;
            if (segs.Length == 1 && int.TryParse(segs[0], out var same)) return same;
        }
        return null;
    }

    private static Dictionary<string, string> ReadDotEnvBesideCompose(string composePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(composePath);
            if (dir is null) return new();
            var envPath = Path.Combine(dir, ".env");
            return File.Exists(envPath) ? EnvFileParser.ReadFile(envPath) : new();
        }
        catch { return new(); }
    }

    private static bool LooksLikePostgresImage(string image)
    {
        var i = image.ToLowerInvariant();
        return i.Contains("postgres") || i.Contains("postgis") || i.Contains("timescale")
            || i.Contains("pgvector") || i.Contains("supabase/postgres") || i.StartsWith("citusdata/");
    }

    private static YamlNode? Get(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;
}

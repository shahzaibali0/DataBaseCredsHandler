using System.Text.Json;
using DbConnectHub.Core.Util;

namespace DbConnectHub.Scanner.Parsers;

/// <summary>
/// Reads .NET appsettings.json (and appsettings.*.json). Walks the JSON tree and treats any
/// string under a "ConnectionStrings" object, or any property named like a connection string,
/// as a candidate ADO.NET connection string.
/// </summary>
public sealed class AppSettingsJsonParser : IConfigParser
{
    public string Kind => "appsettings.json";

    public bool CanParse(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        return n == "appsettings.json" || (n.StartsWith("appsettings.") && n.EndsWith(".json"));
    }

    public IEnumerable<ParsedCredential> Parse(ParseContext context)
    {
        var results = new List<ParsedCredential>();
        JsonDocument doc;
        try
        {
            var text = File.ReadAllText(context.FilePath);
            doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch { return results; }

        var usesNpgsql = context.ProjectUsesNpgsql();
        using (doc)
            Walk(doc.RootElement, "", inConnStrings: false, usesNpgsql, results);
        return results;
    }

    private static void Walk(JsonElement el, string path, bool inConnStrings, bool usesNpgsql, List<ParsedCredential> results)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    var childPath = path.Length == 0 ? prop.Name : $"{path}:{prop.Name}";
                    var nowInConn = inConnStrings || prop.Name.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase);
                    Walk(prop.Value, childPath, nowInConn, usesNpgsql, results);
                }
                break;

            case JsonValueKind.String:
                var value = el.GetString();
                if (string.IsNullOrWhiteSpace(value)) break;
                var looksRelevant = inConnStrings || NameSuggestsConnection(path);
                if (!looksRelevant) break;

                if (PostgresUrl.LooksLikePostgresUrl(value) && PostgresUrl.TryParse(value, out var urlParts) && !urlParts.IsEmpty)
                    results.Add(new ParsedCredential(path, urlParts));
                else if (KeyValueConnectionString.TryParsePostgres(value, usesNpgsql, out var kvParts))
                    results.Add(new ParsedCredential(path, kvParts));
                break;
        }
    }

    private static bool NameSuggestsConnection(string path)
    {
        var p = path.ToLowerInvariant();
        return p.Contains("connectionstring") || p.EndsWith(":database") || p.Contains("npgsql")
               || p.Contains("postgres");
    }
}

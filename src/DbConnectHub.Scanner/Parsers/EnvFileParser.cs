using System.Text;

namespace DbConnectHub.Scanner.Parsers;

/// <summary>Reads .env, .env.local, .env.development, *.env, .env.example …</summary>
public sealed class EnvFileParser : IConfigParser
{
    public string Kind => ".env";

    public bool CanParse(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        return n == ".env" || n.StartsWith(".env.") || n.EndsWith(".env");
    }

    public static bool IsExampleFile(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        return n.Contains("example") || n.Contains("sample") || n.Contains("template") || n.EndsWith(".dist");
    }

    public IEnumerable<ParsedCredential> Parse(ParseContext context)
    {
        var vars = ReadFile(context.FilePath);
        return EnvCredentialExtractor.Extract(vars);
    }

    public static Dictionary<string, string> ReadFile(string path)
    {
        var text = File.ReadAllText(path);
        return ParseText(text);
    }

    /// <summary>Parses dotenv syntax and expands ${VAR} references inside the same file.</summary>
    public static Dictionary<string, string> ParseText(string text)
    {
        var vars = new Dictionary<string, string>(StringComparer.Ordinal);
        var expandable = new HashSet<string>(StringComparer.Ordinal);

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var li = 0; li < lines.Length; li++)
        {
            var line = lines[li].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            if (!IsValidKey(key)) continue;
            var raw = line[(eq + 1)..].TrimStart();

            string value;
            if (raw.StartsWith('"') || raw.StartsWith('\'') || raw.StartsWith('`'))
            {
                var q = raw[0];
                var body = raw[1..];
                var sb = new StringBuilder();
                var closed = false;

                // Quoted values may span several lines.
                while (true)
                {
                    var end = FindClosingQuote(body, q);
                    if (end >= 0)
                    {
                        sb.Append(body[..end]);
                        closed = true;
                        break;
                    }
                    sb.Append(body);
                    if (li + 1 >= lines.Length) break;
                    sb.Append('\n');
                    body = lines[++li];
                }

                value = sb.ToString();
                if (q == '"')
                {
                    value = value.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                    expandable.Add(key);
                }
                if (!closed) value = value.TrimEnd();
            }
            else
            {
                // Inline comment: " #" (a '#' directly inside the value is kept, e.g. passwords).
                var hash = raw.IndexOf(" #", StringComparison.Ordinal);
                value = (hash >= 0 ? raw[..hash] : raw).Trim();
                expandable.Add(key);
            }

            vars[key] = value;
        }

        // Expand references (single-quoted values stay literal).
        foreach (var key in vars.Keys.ToList())
            if (expandable.Contains(key))
                vars[key] = VariableExpander.Expand(vars[key], vars);

        return vars;
    }

    private static int FindClosingQuote(string s, char q)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && q == '"') { i++; continue; }
            if (s[i] == q) return i;
        }
        return -1;
    }

    private static bool IsValidKey(string key) =>
        key.Length > 0 && (char.IsLetter(key[0]) || key[0] == '_') && key.All(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '-');
}

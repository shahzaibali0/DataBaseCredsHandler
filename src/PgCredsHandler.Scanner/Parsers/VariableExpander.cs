using System.Text.RegularExpressions;

namespace PgCredsHandler.Scanner.Parsers;

/// <summary>
/// Expands ${VAR}, ${VAR:-default}, ${VAR-default}, ${VAR:?error} and $VAR the way
/// docker compose and dotenv-expand do. Unknown variables are left untouched.
/// </summary>
public static partial class VariableExpander
{
    [GeneratedRegex(@"\$\$|\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:(?<op>:?[-?+])(?<arg>[^}]*))?\}|\$(?<bare>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex VarRegex();

    public static string Expand(string value, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains('$')) return value;

        var current = value;
        for (var pass = 0; pass < 5; pass++)
        {
            var next = VarRegex().Replace(current, m =>
            {
                if (m.Value == "$$") return "$$"; // keep escapes until the final pass
                var name = m.Groups["name"].Success ? m.Groups["name"].Value : m.Groups["bare"].Value;
                var op = m.Groups["op"].Value;
                var arg = m.Groups["arg"].Value;
                var has = vars.TryGetValue(name, out var v);
                var empty = !has || string.IsNullOrEmpty(v);

                switch (op)
                {
                    case ":-": return empty ? arg : v!;
                    case "-": return has ? v! : arg;
                    case ":+": return empty ? "" : arg;
                    case "+": return has ? arg : "";
                    case ":?":
                    case "?":
                        return has ? v! : m.Value;
                    default:
                        return has ? v! : m.Value;
                }
            });
            if (next == current) break;
            current = next;
        }
        return current.Replace("$$", "$");
    }

    /// <summary>True when a value still contains an unresolved ${...} / $VAR reference.</summary>
    public static bool HasUnresolved(string? value) =>
        value is not null && VarRegex().Matches(value).Any(m => m.Value != "$$");
}

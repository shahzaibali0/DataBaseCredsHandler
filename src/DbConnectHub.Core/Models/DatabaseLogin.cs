using System.Globalization;
using System.Text;

namespace DbConnectHub.Core.Models;

/// <summary>
/// One database and the login used for it (host, port, user, database): what a pgAdmin server entry stands for. Its
/// key is plain hex, so it can be put into a command line run by a shell without any value from a config file adding
/// shell syntax.
/// </summary>
public sealed record DatabaseLogin(string Host, int Port, string Username, string Database)
{
    private const char Separator = '\n';
    private const int PartCount = 4;

    public string ToKey() =>
        Convert.ToHexString(Encoding.UTF8.GetBytes(string.Join(Separator, Host, Port, Username, Database))).ToLowerInvariant();

    public static bool TryParseKey(string key, out DatabaseLogin login)
    {
        login = new DatabaseLogin("", 0, "", "");
        var parts = TryDecodeHex(key)?.Split(Separator);
        if (parts is not { Length: PartCount }) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port)) return false;

        login = new DatabaseLogin(parts[0], port, parts[2], parts[3]);
        return true;
    }

    private static string? TryDecodeHex(string key)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromHexString(key));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

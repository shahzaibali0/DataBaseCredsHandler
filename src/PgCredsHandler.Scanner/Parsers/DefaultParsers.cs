namespace PgCredsHandler.Scanner.Parsers;

public static class DefaultParsers
{
    /// <summary>The parsers the scanner runs, in order. Add new formats here.</summary>
    public static IReadOnlyList<IConfigParser> All { get; } = new IConfigParser[]
    {
        new EnvFileParser(),
        new DockerComposeParser(),
        new AppSettingsJsonParser(),
    };
}

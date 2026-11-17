using DbConnectHub.Core.Util;

namespace DbConnectHub.Scanner.Parsers;

/// <summary>One Postgres connection read out of a config file. Missing parts get defaults later.</summary>
public sealed record ParsedCredential(string SourceKey, PgParts Parts);

public sealed class ParseContext
{
    public required string FilePath { get; init; }
    public required string ProjectRoot { get; init; }

    /// <summary>Lazily checks whether any .csproj in the project references Npgsql.</summary>
    public Func<bool> ProjectUsesNpgsql { get; init; } = () => false;
}

/// <summary>
/// Add a new config format by implementing this and registering it in <see cref="DefaultParsers"/>.
/// </summary>
public interface IConfigParser
{
    /// <summary>Short label shown in the UI, e.g. ".env".</summary>
    string Kind { get; }

    bool CanParse(string fileName);

    IEnumerable<ParsedCredential> Parse(ParseContext context);
}

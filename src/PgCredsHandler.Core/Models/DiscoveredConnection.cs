using PgCredsHandler.Core.Util;

namespace PgCredsHandler.Core.Models;

/// <summary>
/// A Postgres connection found by the scanner in a project's config file.
/// This is a transient object: it carries the password in memory only until
/// it is merged into the saved list and the password is written to the credential store.
/// </summary>
public sealed class DiscoveredConnection
{
    public required string ProjectName { get; init; }
    public required string ProjectPath { get; init; }
    public required string SourceFile { get; init; }

    /// <summary>Short label of the file type, e.g. ".env", "docker-compose", "appsettings.json".</summary>
    public required string SourceKind { get; init; }

    /// <summary>Where inside the file it came from, e.g. "DATABASE_URL" or "service: db".</summary>
    public string? SourceKey { get; init; }

    /// <summary>True for .env.example / .env.sample style files (lower priority when de-duplicating).</summary>
    public bool IsExampleFile { get; init; }

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5432;
    public string Database { get; init; } = "postgres";
    public string Username { get; init; } = "postgres";
    public string? Password { get; init; }

    public string Fingerprint => ConnectionKey.Create(ProjectPath, Host, Port, Database, Username);

    public override string ToString() => $"{ProjectName}: {Username}@{Host}:{Port}/{Database} ({SourceKind} {SourceKey})";
}

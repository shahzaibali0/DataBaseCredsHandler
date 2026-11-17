using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Util;

namespace PgCredsHandler.Presentation;

/// <summary>One database row. Holds no password, only whether one is stored.</summary>
public sealed class ConnectionRowViewModel
{
    private ConnectionRowViewModel(SavedConnection connection)
    {
        Id = connection.Id;
        ProjectName = connection.ProjectName;
        ProjectPath = connection.ProjectPath;
        Database = connection.Database;
        Username = connection.Username;
        Host = connection.Host;
        Endpoint = $"{connection.Username}@{connection.Host}:{connection.Port}";
        HostKind = HostClassifier.Classify(connection.Host);
        Source = string.IsNullOrEmpty(connection.SourceKey)
            ? connection.SourceKind
            : $"{connection.SourceKind} · {connection.SourceKey}";
        SourceFile = connection.SourceFile;
        IsStale = connection.IsStale;
        HasPassword = connection.HasPassword;
    }

    public string Id { get; }

    public string ProjectName { get; }

    public string ProjectPath { get; }

    public string Database { get; }

    public string Username { get; }

    public string Host { get; }

    /// <summary><c>user@host:port</c>, as people write it.</summary>
    public string Endpoint { get; }

    public HostKind HostKind { get; }

    public string HostKindLabel => HostKind switch
    {
        HostKind.Local => "Local",
        HostKind.DockerService => "Docker",
        HostKind.Private => "Private network",
        _ => "Remote",
    };

    /// <summary>True for hosts on this PC (localhost or a Docker service), false for anything on a network.</summary>
    public bool IsOnThisPc => HostKind is HostKind.Local or HostKind.DockerService;

    public string Source { get; }

    public string SourceFile { get; }

    /// <summary>Endpoint and source on one line, for the row's description.</summary>
    public string Description => $"{Endpoint}  ·  {Source}";

    public bool IsStale { get; }

    public bool HasPassword { get; }

    public string PasswordLabel => HasPassword ? "Password saved" : "No password found";

    public static ConnectionRowViewModel From(SavedConnection connection) => new(connection);

    /// <summary>True when the search text appears in the project, host, database or user. Never the password.</summary>
    public bool Matches(string searchText) =>
        string.IsNullOrWhiteSpace(searchText)
        || new[] { ProjectName, Host, Database, Username }
            .Any(field => field.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase));
}

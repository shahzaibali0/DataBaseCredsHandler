using System.Text.Json.Serialization;
using DbConnectHub.Core.Util;

namespace DbConnectHub.Core.Models;

/// <summary>
/// A connection saved by the tool. Contains NO password: the password lives in
/// Windows Credential Manager under the key built from <see cref="Id"/>.
/// </summary>
public sealed class SavedConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string ProjectName { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string? SourceKey { get; set; }

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "postgres";
    public string Username { get; set; } = "postgres";

    /// <summary>True when a password is stored in the credential store for this entry.</summary>
    public bool HasPassword { get; set; }

    /// <summary>Added or edited by hand. Scans never overwrite or mark manual entries stale.</summary>
    public bool IsManual { get; set; }

    /// <summary>The last scan of its folder did not find this connection any more.</summary>
    public bool IsStale { get; set; }

    /// <summary>Set when a scan found a different password than the one stored.</summary>
    public DateTimeOffset? PasswordChangedAt { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.Now;

    public string? Notes { get; set; }

    [JsonIgnore]
    public string Fingerprint => ConnectionKey.Create(ProjectPath, Host, Port, Database, Username);

    public SavedConnection Clone() => (SavedConnection)MemberwiseClone();
}

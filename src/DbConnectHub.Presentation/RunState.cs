namespace DbConnectHub.Presentation;

/// <summary>Whether a local dev server or database is running right now.</summary>
public enum RunState
{
    /// <summary>Not checked yet, or not on this PC (a remote host is never checked).</summary>
    Unknown,
    Running,
    Stopped,
}

/// <summary>Checks a port on this PC (loopback only). Implemented in DbConnectHub.Integrations.</summary>
public interface ILocalPortProbe
{
    Task<bool> IsListeningAsync(int port, CancellationToken cancellationToken);
}

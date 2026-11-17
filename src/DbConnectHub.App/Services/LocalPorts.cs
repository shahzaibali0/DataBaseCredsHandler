using DbConnectHub.Integrations;
using DbConnectHub.Presentation;

namespace DbConnectHub.App.Services;

/// <summary>Connects the running/stopped dots to the loopback-only port check in Integrations.</summary>
public sealed class LocalPorts : ILocalPortProbe
{
    public Task<bool> IsListeningAsync(int port, CancellationToken cancellationToken) =>
        LocalPortProbe.IsListeningAsync(port, LocalPortProbe.DefaultTimeout, cancellationToken);
}

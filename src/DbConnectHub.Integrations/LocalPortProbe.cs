using System.Net;
using System.Net.Sockets;

namespace DbConnectHub.Integrations;

/// <summary>
/// Tells whether something is listening on a port of THIS PC, to show a dev server or local database as running.
/// It only ever connects to the loopback address (127.0.0.1 / ::1), never to another machine, and sends nothing:
/// the connection is opened and closed straight away.
/// </summary>
public static class LocalPortProbe
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(300);

    public static async Task<bool> IsListeningAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (port is <= 0 or > 65535) return false;
        return await TryConnectAsync(IPAddress.Loopback, port, timeout, cancellationToken)
               || await TryConnectAsync(IPAddress.IPv6Loopback, port, timeout, cancellationToken);
    }

    private static async Task<bool> TryConnectAsync(IPAddress address, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var client = new TcpClient(address.AddressFamily);
        try
        {
            await client.ConnectAsync(address, port, deadline.Token);
            return true;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return false; // refused or no answer in time: nothing is running there
        }
    }
}

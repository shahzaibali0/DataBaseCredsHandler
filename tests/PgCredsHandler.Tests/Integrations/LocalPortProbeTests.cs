using System.Net;
using System.Net.Sockets;
using PgCredsHandler.Integrations;

namespace PgCredsHandler.Tests.Integrations;

public sealed class LocalPortProbeTests
{
    [Fact]
    public async Task IsListening_SomethingListensOnThePort_ReturnsTrue()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            Assert.True(await LocalPortProbe.IsListeningAsync(port, TimeSpan.FromSeconds(2), CancellationToken.None));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task IsListening_NothingOnThePort_ReturnsFalse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop(); // the port is now free

        Assert.False(await LocalPortProbe.IsListeningAsync(port, TimeSpan.FromMilliseconds(500), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public async Task IsListening_InvalidPort_ReturnsFalseWithoutConnecting(int port)
    {
        Assert.False(await LocalPortProbe.IsListeningAsync(port, TimeSpan.FromMilliseconds(100), CancellationToken.None));
    }
}

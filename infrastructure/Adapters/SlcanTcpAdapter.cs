using System.Net.Sockets;

namespace infrastructure.Adapters;

// SLCAN over a TCP socket (ser2net, ESP32 bridge, simulator). Port is host:port, tcp:// prefix optional.
public class SlcanTcpAdapter : SlcanAdapter
{
    public override string Name => "SLCAN TCP";

    private TcpClient? _client;

    protected override async Task<Stream> OpenTransportAsync(string port, CancellationToken ct)
    {
        var uri = new Uri(port.Contains("://") ? port : "tcp://" + port);
        if (uri.Port <= 0) throw new FormatException($"'{port}' is missing a port (host:port)");

        _client = new TcpClient { NoDelay = true };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await _client.ConnectAsync(uri.DnsSafeHost, uri.Port, timeout.Token);
        return _client.GetStream();
    }

    protected override void CloseTransport()
    {
        base.CloseTransport();
        _client?.Dispose();
        _client = null;
    }
}

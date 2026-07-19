using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace WebSockets.FakeServer;

/// <summary>
/// A minimal, deliberately dumb WebSocket server used to reproduce and verify
/// fixes for ClientWebSocket's strict <c>Connection</c> response-header
/// validation, and to exercise other handshake-response validation paths via
/// <see cref="FakeWebSocketServerResponse"/>. By default, accepts exactly one
/// connection, sends a hand-crafted response, then echoes back the first
/// text frame it receives before closing. Optionally speaks TLS
/// (<c>wss://</c>) if a server certificate is supplied, entirely separate
/// from and prior to the handshake-response logic above.
///
/// Pass a <see cref="FakeWebSocketServerConnectionOptions"/> with
/// <c>SupportsReconnection: true</c> for resilience testing: the server then
/// keeps accepting new connections after each one ends, each connection
/// runs a continuous echo loop rather than a single message,
/// <see cref="DropCurrentConnectionAsync"/> lets a test abruptly kill the
/// current connection on demand, <see cref="FakeWebSocketServerConnectionOptions.FailFirstConnections"/>
/// simulates a server that's briefly unreachable, and
/// <see cref="FakeWebSocketServerConnectionOptions.FragmentEchoIntoChunksOfSize"/>
/// exercises fragmented-message reassembly.
/// </summary>
public sealed class FakeWebSocketServer : IAsyncDisposable
{
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly Task _acceptLoop;
    private readonly FakeWebSocketServerConnectionOptions _connectionOptions;

    private volatile TcpClient? _currentClient;
    private int _connectionCount;
    private int _disposed;
    private volatile string? _lastMessageReceived;

    /// <summary>The loopback port this server is listening on.</summary>
    public int Port { get; }

    /// <summary>
    /// The URI clients should connect to: <c>ws://</c> normally, or
    /// <c>wss://</c> if a server certificate was supplied to <see cref="StartAsync(string,X509Certificate2?,TimeSpan?,FakeWebSocketServerConnectionOptions?,CancellationToken)"/>.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// The number of connections accepted so far, including any that were
    /// immediately aborted via <see cref="FakeWebSocketServerConnectionOptions.FailFirstConnections"/>,
    /// and including the current one if any. Only meaningful for resilience
    /// testing (<c>SupportsReconnection: true</c>); otherwise it's always 0 or 1.
    /// </summary>
    public int ConnectionCount => _connectionCount;

    /// <summary>
    /// The most recent message this server has read from a client, set the
    /// moment it's read, independent of whether it's been echoed back yet or
    /// whether the client is even still around to receive that echo. Useful
    /// for verifying a message was actually received without depending on
    /// the client's own receive loop surviving long enough to process the
    /// response.
    /// </summary>
    public string? LastMessageReceived => _lastMessageReceived;

    private FakeWebSocketServer(
        TcpListener listener,
        FakeWebSocketServerResponse response,
        X509Certificate2? serverCertificate,
        TimeSpan timeout,
        FakeWebSocketServerConnectionOptions connectionOptions,
        CancellationToken externalToken)
    {
        _listener = listener;
        _connectionOptions = connectionOptions;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var scheme = serverCertificate is not null ? "wss" : "ws";
        Uri = new Uri($"{scheme}://127.0.0.1:{Port}/");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _cts.CancelAfter(timeout);

        _acceptLoop = Task.Run(() => AcceptLoopAsync(response, serverCertificate, _cts.Token));
    }

    /// <summary>
    /// Convenience overload for the common case: everything about the
    /// response is correct/well-formed except the <c>Connection</c> header.
    /// </summary>
    /// <param name="connectionHeaderValue">
    /// The exact value to send back as the <c>Connection</c> response header,
    /// e.g. <c>"Upgrade"</c> or <c>"Upgrade, Keep-Alive"</c>.
    /// </param>
    /// <param name="serverCertificate">
    /// If supplied, the server speaks TLS (<c>wss://</c>), presenting this
    /// certificate during the SSL handshake. If omitted, the server speaks
    /// plain <c>ws://</c>.
    /// </param>
    /// <param name="timeout">
    /// How long to wait for the accept/handshake/echo sequence to complete
    /// before tearing down the listener. Defaults to 5 seconds.
    /// </param>
    /// <param name="connectionOptions">Controls behavior across the connection lifecycle; see <see cref="FakeWebSocketServerConnectionOptions"/>.</param>
    /// <param name="cancellationToken">Cancels the accept/handshake/echo sequence early.</param>
    public static Task<FakeWebSocketServer> StartAsync(
        string connectionHeaderValue,
        X509Certificate2? serverCertificate = null,
        TimeSpan? timeout = null,
        FakeWebSocketServerConnectionOptions? connectionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var response = new FakeWebSocketServerResponse { ConnectionHeaderValue = connectionHeaderValue };
        return StartAsync(response, serverCertificate, timeout, connectionOptions, cancellationToken);
    }

    /// <summary>
    /// Starts listening on an OS-assigned loopback port and returns once the
    /// server is ready to accept a connection. The accept/handshake/echo
    /// sequence runs in the background; if it hasn't completed within
    /// <paramref name="timeout"/> (default 5 seconds), the listener is torn
    /// down so a broken client can't hang the test suite.
    /// </summary>
    /// <param name="response">Full control over the handshake response.</param>
    /// <param name="serverCertificate">
    /// If supplied, the server speaks TLS (<c>wss://</c>), presenting this
    /// certificate during the SSL handshake, before any of the plaintext
    /// HTTP upgrade exchange happens. If omitted, the server speaks plain
    /// <c>ws://</c>. This library generates no certificates itself; see
    /// <see cref="SelfSignedCertificateFactory"/>.
    /// </param>
    /// <param name="timeout">
    /// How long to wait for the accept/handshake/echo sequence to complete
    /// before tearing down the listener. Defaults to 5 seconds.
    /// </param>
    /// <param name="connectionOptions">Controls behavior across the connection lifecycle; see <see cref="FakeWebSocketServerConnectionOptions"/>.</param>
    /// <param name="cancellationToken">Cancels the accept/handshake/echo sequence early.</param>
    public static Task<FakeWebSocketServer> StartAsync(
        FakeWebSocketServerResponse response,
        X509Certificate2? serverCertificate = null,
        TimeSpan? timeout = null,
        FakeWebSocketServerConnectionOptions? connectionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var server = new FakeWebSocketServer(
            listener,
            response,
            serverCertificate,
            timeout ?? DefaultTimeout,
            connectionOptions ?? new FakeWebSocketServerConnectionOptions(),
            cancellationToken);

        return Task.FromResult(server);
    }

    /// <summary>
    /// Forcibly, abruptly terminates the current connection, an RST rather
    /// than a graceful close, simulating a real network/server failure
    /// rather than a clean shutdown. Only meaningful when this server was
    /// started with <c>SupportsReconnection: true</c>; after the drop, the
    /// accept loop goes back to waiting for a new connection. No-op if
    /// nothing is currently connected.
    /// </summary>
    public Task DropCurrentConnectionAsync()
    {
        var client = _currentClient;
        if (client is not null)
        {
            AbortConnection(client);
        }

        return Task.CompletedTask;
    }

    private static void AbortConnection(TcpClient client)
    {
        try
        {
            // A zero-second LingerState forces an abortive close (RST)
            // instead of a graceful FIN-based shutdown, so the client sees a
            // genuine connection reset rather than a clean EOF.
            client.Client.LingerState = new LingerOption(true, 0);
            client.Close();
        }
        catch (ObjectDisposedException)
        {
            // Already gone; nothing left to do.
        }
        catch (SocketException)
        {
            // Already in a bad state; the point was to kill it anyway.
        }
    }

    private async Task AcceptLoopAsync(FakeWebSocketServerResponse response, X509Certificate2? serverCertificate, CancellationToken cancellationToken)
    {
        do
        {
            using var client = await AcceptClientAsync(cancellationToken).ConfigureAwait(false);
            if (client is null)
            {
                return;
            }

            var connectionNumber = Interlocked.Increment(ref _connectionCount);
            _currentClient = client;

            try
            {
                // connectionNumber == 1 is always the initial connection and
                // is never failed; FailFirstConnections applies to the N
                // reconnect attempts immediately following it.
                if (connectionNumber > 1 && connectionNumber <= _connectionOptions.FailFirstConnections + 1)
                {
                    // Simulates a server that's briefly unreachable during a
                    // reconnect: the connection is accepted at the TCP level
                    // (so it counts toward ConnectionCount) but abandoned
                    // before ever reading the handshake request or sending a
                    // response.
                    AbortConnection(client);
                    continue;
                }

                await HandleConnectionAsync(client, response, serverCertificate, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The connection ended abruptly, e.g. DropCurrentConnectionAsync()
                // or a genuine network failure mid-read/write. Treat it the
                // same as a graceful end so the loop continues to the next
                // connection instead of the whole accept loop faulting.
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
            finally
            {
                _currentClient = null;
            }
        }
        while (_connectionOptions.SupportsReconnection && !cancellationToken.IsCancellationRequested);
    }

    private async Task HandleConnectionAsync(
        TcpClient client,
        FakeWebSocketServerResponse response,
        X509Certificate2? serverCertificate,
        CancellationToken cancellationToken)
    {
        using var networkStream = client.GetStream();
        using var socketCloseRegistration = cancellationToken.Register(() =>
        {
            try
            {
                client.Close();
            }
            catch (ObjectDisposedException)
            {
                // Already closed; nothing left to do.
            }
        });

        SslStream? sslStream = null;
        Stream stream = networkStream;

        try
        {
            if (serverCertificate is not null)
            {
                // leaveInnerStreamOpen: true because networkStream's own
                // `using` above already owns disposing the underlying socket.
                sslStream = new SslStream(networkStream, leaveInnerStreamOpen: true);
                await sslStream.AuthenticateAsServerAsync(serverCertificate).ConfigureAwait(false);
                stream = sslStream;
            }

            var secWebSocketKey = await ReadHandshakeRequestAsync(stream, cancellationToken).ConfigureAwait(false);
            if (secWebSocketKey is null)
            {
                return;
            }

            await WriteHandshakeResponseAsync(stream, secWebSocketKey, response, cancellationToken).ConfigureAwait(false);

            if (!_connectionOptions.SupportsReconnection)
            {
                // Original behavior: exactly one echo, then the connection ends.
                var message = await ReadTextFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (message is not null)
                {
                    _lastMessageReceived = message;
                    await WriteTextFrameAsync(stream, message, _connectionOptions.FragmentEchoIntoChunksOfSize, cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            // Resilience-testing mode: keep echoing until the connection ends,
            // whether that's the client closing, DropCurrentConnectionAsync()
            // yanking the socket, or cancellation.
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await ReadTextFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (message is null)
                {
                    return;
                }

                _lastMessageReceived = message;
                await WriteTextFrameAsync(stream, message, _connectionOptions.FragmentEchoIntoChunksOfSize, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            sslStream?.Dispose();
        }
    }

    private async Task<TcpClient?> AcceptClientAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException)
            {
                // Already stopped by Dispose; nothing left to do.
            }
        });

        try
        {
            return await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static async Task<string?> ReadHandshakeRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Byte-at-a-time is wasteful in general, but this is a test double
        // reading a few hundred bytes at most, simplicity wins here. We only
        // read far enough to find the blank line; we're not validating the
        // request, just pulling the one header we need to compute the accept key.
        var buffer = new List<byte>();
        var chunk = new byte[1];

        while (!EndsWithBlankLine(buffer))
        {
            int read = await stream.ReadAsync(chunk, 0, 1, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            buffer.Add(chunk[0]);
        }

        var requestText = Encoding.ASCII.GetString(buffer.ToArray());
        var lines = requestText.Split(new[] { "\r\n" }, StringSplitOptions.None);

        const string secWebSocketKeyHeader = "Sec-WebSocket-Key:";
        foreach (var line in lines)
        {
            if (line.StartsWith(secWebSocketKeyHeader, StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(secWebSocketKeyHeader.Length).Trim();
            }
        }

        return null;
    }

    private static bool EndsWithBlankLine(List<byte> buffer)
    {
        const string terminator = "\r\n\r\n";
        if (buffer.Count < terminator.Length)
        {
            return false;
        }

        for (int i = 0; i < terminator.Length; i++)
        {
            if (buffer[buffer.Count - terminator.Length + i] != (byte)terminator[i])
            {
                return false;
            }
        }

        return true;
    }

    private static async Task WriteHandshakeResponseAsync(
        Stream stream,
        string secWebSocketKey,
        FakeWebSocketServerResponse response,
        CancellationToken cancellationToken)
    {
        var accept = response.AcceptHeaderOverride ?? ComputeAcceptKey(secWebSocketKey);

        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {response.StatusCode} {response.StatusDescription}\r\n");

        if (response.UpgradeHeaderValue is not null)
        {
            sb.Append($"Upgrade: {response.UpgradeHeaderValue}\r\n");
        }

        sb.Append($"Connection: {response.ConnectionHeaderValue}\r\n");

        if (response.SubProtocol is not null)
        {
            sb.Append($"Sec-WebSocket-Protocol: {response.SubProtocol}\r\n");
        }

        sb.Append($"Sec-WebSocket-Accept: {accept}\r\n");
        sb.Append("\r\n");

        var bytes = Encoding.ASCII.GetBytes(sb.ToString());
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
    }

    private static string ComputeAcceptKey(string secWebSocketKey)
    {
        using var sha1 = SHA1.Create();
        var bytes = Encoding.ASCII.GetBytes(secWebSocketKey + WebSocketGuid);
        return Convert.ToBase64String(sha1.ComputeHash(bytes));
    }

    // Single-frame, non-fragmented text-frame support only for reading: enough
    // to prove the connection is alive and usable, not a general-purpose
    // WebSocket implementation. The client in our tests always sends one
    // small frame, so this doesn't need to handle fragmented *incoming*
    // messages (only fragmented *outgoing* ones, via WriteTextFrameAsync below).
    private static async Task<string?> ReadTextFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[2];
        if (!await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        bool masked = (header[1] & 0b1000_0000) != 0;
        int payloadLength = header[1] & 0b0111_1111;

        if (payloadLength == 126)
        {
            var extended = new byte[2];
            await ReadExactAsync(stream, extended, cancellationToken).ConfigureAwait(false);
            payloadLength = (extended[0] << 8) | extended[1];
        }
        else if (payloadLength == 127)
        {
            var extended = new byte[8];
            await ReadExactAsync(stream, extended, cancellationToken).ConfigureAwait(false);
            // Test double: never expecting anywhere near this large a message.
            payloadLength = checked((int)(((long)extended[4] << 24) | ((long)extended[5] << 16) | ((long)extended[6] << 8) | extended[7]));
        }

        var maskKey = Array.Empty<byte>();
        if (masked)
        {
            maskKey = new byte[4];
            await ReadExactAsync(stream, maskKey, cancellationToken).ConfigureAwait(false);
        }

        var payload = new byte[payloadLength];
        await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);

        if (masked)
        {
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] ^= maskKey[i % 4];
            }
        }

        return Encoding.UTF8.GetString(payload);
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    // If chunkSize is null (the common case), the message goes out as a
    // single complete frame, unchanged from the original behavior. If set,
    // it's split across multiple frames (first: text/FIN=0, middle:
    // continuation/FIN=0, last: continuation/FIN=1), a real fragmented
    // message, to exercise a client's reassembly logic.
    private static async Task WriteTextFrameAsync(Stream stream, string message, int? chunkSize, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(message);

        if (chunkSize is null || payload.Length <= chunkSize.Value)
        {
            await WriteFrameAsync(stream, opcode: 0x1, fin: true, payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            return;
        }

        var offset = 0;
        var isFirst = true;

        while (offset < payload.Length)
        {
            var length = Math.Min(chunkSize.Value, payload.Length - offset);
            var isLast = offset + length >= payload.Length;
            byte opcode = isFirst ? (byte)0x1 : (byte)0x0;

            await WriteFrameAsync(stream, opcode, isLast, payload, offset, length, cancellationToken).ConfigureAwait(false);

            offset += length;
            isFirst = false;
        }
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        byte opcode,
        bool fin,
        byte[] payload,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)((fin ? 0b1000_0000 : 0) | opcode));

        if (count < 126)
        {
            ms.WriteByte((byte)count);
        }
        else if (count <= ushort.MaxValue)
        {
            ms.WriteByte(126);
            ms.WriteByte((byte)(count >> 8));
            ms.WriteByte((byte)(count & 0xFF));
        }
        else
        {
            throw new NotSupportedException("FakeWebSocketServer only supports small test payloads.");
        }

        ms.Write(payload, offset, count);

        var frame = ms.ToArray();
        await stream.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the listener and waits for the background accept loop to finish
    /// (or be cancelled). Safe to call even if no client ever connected, and
    /// safe to call more than once, the second call is a no-op.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Guards against running twice; CancellationTokenSource.Cancel()
        // throws if called after Dispose(), so a second caller (e.g. a test
        // that manually disposes early, then also has this in an
        // `await using`) needs to be a no-op rather than throw.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();

        try
        {
            _listener.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already stopped via the cancellation registration; fine.
        }

        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch
        {
            // Shutdown shouldn't surface accept-loop exceptions to a caller
            // that's just cleaning up after a test.
        }

        _cts.Dispose();
    }
}
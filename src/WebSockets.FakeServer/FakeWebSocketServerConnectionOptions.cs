namespace WebSockets.FakeServer;

/// <summary>
/// Controls how <see cref="FakeWebSocketServer"/> behaves across the
/// connection lifecycle, as opposed to <see cref="FakeWebSocketServerResponse"/>,
/// which controls what the handshake response itself looks like.
/// </summary>
public sealed class FakeWebSocketServerConnectionOptions
{
    /// <summary>
    /// If true, the server keeps accepting new connections after each one
    /// ends, runs a continuous echo loop per connection instead of a single
    /// message, and <see cref="FakeWebSocketServer.DropCurrentConnectionAsync"/>
    /// becomes usable. Defaults to false: the original single-connection,
    /// single-echo behavior.
    /// </summary>
    public bool SupportsReconnection { get; set; } = false;

    /// <summary>
    /// The number of reconnect attempts (i.e. connections after the first)
    /// to abruptly abort (RST) before ever reading the handshake request,
    /// simulating a server that's briefly unreachable during a reconnect.
    /// The initial connection is never affected by this, only the
    /// <c>N</c> connection attempts immediately following it; those still
    /// count toward <see cref="FakeWebSocketServer.ConnectionCount"/> but
    /// otherwise behave normally. Defaults to 0 (no failures). Only
    /// meaningful alongside <see cref="SupportsReconnection"/>; without it,
    /// there's no accept loop for a later attempt to succeed in.
    /// </summary>
    public int FailFirstConnections { get; set; } = 0;

    /// <summary>
    /// If set, the echoed response is split across multiple WebSocket frames
    /// of at most this many bytes each (a real, fragmented message), instead
    /// of always sending it as a single complete frame. Defaults to null
    /// (single frame).
    /// </summary>
    public int? FragmentEchoIntoChunksOfSize { get; set; } = null;
}
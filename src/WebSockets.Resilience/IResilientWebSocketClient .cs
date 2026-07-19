namespace WebSockets.Resilience;

/// <summary>
/// Abstraction over <see cref="ResilientWebSocketClient"/>, primarily so
/// consumers can mock it in unit tests, the same reasoning as
/// <see cref="IWebSocketClient"/> for <see cref="WebSocketClient"/>.
/// </summary>
public interface IResilientWebSocketClient : IAsyncDisposable
{
    /// <summary>Configuration for this client.</summary>
    ResilientWebSocketClientOptions Options { get; }

    /// <summary>
    /// Raised when a reconnect attempt (not the initial <see cref="StartAsync"/>
    /// connection) fails. The client keeps retrying regardless; this is
    /// purely observational.
    /// </summary>
    event Action<Exception>? ReconnectFailed;

    /// <summary>
    /// Raised after a reconnect succeeds, not after the initial connection
    /// established by <see cref="StartAsync"/>. Since a new connection has no
    /// memory of the previous session, use this to resubscribe to whatever
    /// channels/topics/state the server needs re-established.
    /// </summary>
    event Action? Reconnected;

    /// <summary>Raised once per fully-reassembled incoming text message.</summary>
    event Action<string>? MessageReceived;

    /// <summary>
    /// Establishes the initial connection and starts the background
    /// reconnect loop. Throws if the initial connection fails; once started,
    /// subsequent reconnect failures are reported via <see cref="ReconnectFailed"/>
    /// instead of thrown. Returns this instance once started, for chaining.
    /// </summary>
    Task<IResilientWebSocketClient> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues <paramref name="message"/> for sending and returns once it's
    /// accepted onto the send queue, not once it's actually been written to
    /// the socket. If currently disconnected, the message waits in the queue
    /// until a connection is available rather than throwing. Returns this
    /// instance, for chaining.
    /// </summary>
    Task<IResilientWebSocketClient> SendAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully shuts down: stops accepting new sends immediately, waits
    /// up to <paramref name="timeout"/> for anything already queued to
    /// actually be sent, then tears down the connection and reconnect loop,
    /// all as one atomic operation, no gap in between where a new message
    /// could still be queued and lost. Unlike <see cref="IAsyncDisposable.DisposeAsync"/>,
    /// which stays fast and abrupt (matching <c>WebSocket.Dispose()</c>'s own
    /// convention), this is the opt-in graceful path, the same relationship
    /// <c>WebSocket.CloseAsync</c> has to <c>WebSocket.Dispose()</c>. Safe to
    /// call before or instead of disposing; disposing afterward is a no-op.
    /// </summary>
    Task CloseAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
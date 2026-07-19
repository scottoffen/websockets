using System.Net.WebSockets;

namespace WebSockets;

/// <summary>
/// Abstraction over <see cref="WebSocketClient"/>, primarily so consumers can
/// mock it in unit tests. <see cref="WebSocketClient"/> is sealed (nothing
/// about this library is meant to be extended via inheritance), so this
/// interface is the substitution point instead, the same pattern commonly
/// used around <c>HttpClient</c> for the same reason.
/// </summary>
public interface IWebSocketClient
{
    /// <summary>Configuration for this client. Set before calling <see cref="ConnectAsync"/>.</summary>
    WebSocketClientOptions Options { get; }

    /// <summary>
    /// Connects to <paramref name="uri"/> and returns the resulting <see cref="WebSocket"/>
    /// once the handshake completes and passes validation.
    /// </summary>
    Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken = default);
}
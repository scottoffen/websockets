using System.Net.WebSockets;

namespace WebSockets;

/// <summary>
/// Configuration for <see cref="WebSocketClient"/>. Mirrors
/// <c>ClientWebSocketOptions</c> where it makes sense to, but exposes every
/// response-validation check as an overridable delegate rather than hardcoding
/// them the way <c>ClientWebSocket.ValidateResponse</c> does internally.
///
/// This shared partial holds everything that's identical across platforms.
/// Platform-specific configuration (request/handler escape hatches, since the
/// underlying transport differs) lives in the Legacy/ and Modern/
/// partials.
/// </summary>
public sealed partial class WebSocketClientOptions
{
    /// <summary>
    /// Sub-protocols to request via the <c>Sec-WebSocket-Protocol</c> header.
    /// If the server responds with a value not in this collection, connecting
    /// fails, this negotiation check itself is not overridable, only which
    /// values you request is.
    /// </summary>
    public ICollection<string> RequestedSubProtocols { get; } = new List<string>();

    /// <summary>Matches <c>ClientWebSocketOptions.KeepAliveInterval</c>'s default.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = WebSocket.DefaultKeepAliveInterval;

    /// <summary>Matches <c>ClientWebSocketOptions</c>'s default receive buffer size.</summary>
    public int ReceiveBufferSize { get; set; } = 16 * 1024;

    /// <summary>Matches <c>ClientWebSocketOptions</c>'s default send buffer size.</summary>
    public int SendBufferSize { get; set; } = 16 * 1024;

    /// <summary>
    /// Validates the <c>Upgrade</c> response header. Defaults to requiring it
    /// equal "websocket" (case-insensitive), per RFC 6455.
    /// </summary>
    public Func<string?, bool> IsValidUpgradeHeader { get; set; } =
        value => string.Equals(value, "websocket", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the <c>Connection</c> response header. This is the actual
    /// fix this library exists for: the default here treats the header as a
    /// comma-separated token list and accepts it as long as "Upgrade" is one
    /// of the tokens, rather than requiring an exact match against the whole
    /// header value the way <c>ClientWebSocket.ValidateResponse</c> does. A
    /// server responding with "Connection: Upgrade, Keep-Alive" passes this
    /// default; it would fail against stock <c>ClientWebSocket</c> on either
    /// .NET Framework or modern .NET.
    /// </summary>
    public Func<string?, bool> IsValidConnectionHeader { get; set; } =
        value => (value ?? string.Empty)
            .Split(',')
            .Select(token => token.Trim())
            .Any(token => string.Equals(token, "Upgrade", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Validates the <c>Sec-WebSocket-Accept</c> response header against the
    /// expected value computed from the request's <c>Sec-WebSocket-Key</c>.
    /// Defaults to an exact, case-insensitive match, per RFC 6455.
    /// </summary>
    public Func<string, string?, bool> IsValidAcceptHeader { get; set; } =
        (expected, actual) => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
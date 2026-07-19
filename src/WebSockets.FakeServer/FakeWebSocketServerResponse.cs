namespace WebSockets.FakeServer;

/// <summary>
/// Full control over the handshake response <see cref="FakeWebSocketServer"/>
/// sends back, for tests that need to exercise validation failure paths
/// beyond just the <c>Connection</c> header: status code, <c>Upgrade</c>
/// header, <c>Sec-WebSocket-Accept</c>, and sub-protocol negotiation.
/// </summary>
public sealed class FakeWebSocketServerResponse
{
    /// <summary>HTTP status code to send. Defaults to 101.</summary>
    public int StatusCode { get; set; } = 101;

    /// <summary>HTTP status line reason phrase. Defaults to "Switching Protocols".</summary>
    public string StatusDescription { get; set; } = "Switching Protocols";

    /// <summary>
    /// The <c>Upgrade</c> response header value. Defaults to "websocket". Set
    /// to null to omit the header entirely.
    /// </summary>
    public string? UpgradeHeaderValue { get; set; } = "websocket";

    /// <summary>The <c>Connection</c> response header value. Defaults to "Upgrade".</summary>
    public string ConnectionHeaderValue { get; set; } = "Upgrade";

    /// <summary>
    /// If set, sent back as the <c>Sec-WebSocket-Protocol</c> response header
    /// (sub-protocol negotiation). Left null, the header is omitted.
    /// </summary>
    public string? SubProtocol { get; set; }

    /// <summary>
    /// If set, sent back as the <c>Sec-WebSocket-Accept</c> header verbatim,
    /// instead of the correctly-computed value. Use this to exercise
    /// accept-header validation failures.
    /// </summary>
    public string? AcceptHeaderOverride { get; set; }
}
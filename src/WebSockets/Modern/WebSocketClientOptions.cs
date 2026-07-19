namespace WebSockets;

public sealed partial class WebSocketClientOptions
{
    /// <summary>
    /// Configures the <see cref="SocketsHttpHandler"/> used for the handshake,
    /// invoked before the request is sent. Use this for anything connection-
    /// level: <see cref="SocketsHttpHandler.Credentials"/>, <see cref="SocketsHttpHandler.Proxy"/>,
    /// <see cref="SocketsHttpHandler.SslOptions"/> (including a custom
    /// <c>RemoteCertificateValidationCallback</c>, the modern equivalent of
    /// <c>ClientWebSocketOptions.RemoteCertificateValidationCallback</c>), and
    /// so on. This is the modern-platform counterpart to the net462 side's
    /// <c>ConfigureRequest</c>: on this platform, connection-level settings
    /// live on the handler, not the request message.
    /// </summary>
    public Action<SocketsHttpHandler>? ConfigureHandler { get; set; }

    /// <summary>
    /// Configures the outgoing <see cref="HttpRequestMessage"/> itself,
    /// invoked after the WebSocket handshake headers are set and before the
    /// request is sent. Use this for custom headers or other per-request
    /// tweaks that aren't connection-level.
    /// </summary>
    public Action<HttpRequestMessage>? ConfigureRequest { get; set; }
}
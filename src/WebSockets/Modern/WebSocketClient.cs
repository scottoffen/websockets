using System.Net;
using System.Net.WebSockets;

namespace WebSockets;

public sealed partial class WebSocketClient
{
    private async partial Task<WebSocket> ConnectAsyncCore(Uri uri, CancellationToken cancellationToken)
    {
        var handler = new SocketsHttpHandler();
        Options.ConfigureHandler?.Invoke(handler);

        var invoker = new HttpMessageInvoker(handler, disposeHandler: true);

        var scheme = string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        var requestUri = new UriBuilder(uri) { Scheme = scheme }.Uri;

        var secWebSocketKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri)
        {
            // WebSocket upgrades over HTTP/1.1 require pinning the version:
            // SocketsHttpHandler may otherwise attempt HTTP/2, which doesn't
            // support this style of upgrade the same way.
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        // Connection/Upgrade aren't addable via the normal, validating header
        // APIs; TryAddWithoutValidation is the standard way around that.
        request.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        request.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        request.Headers.TryAddWithoutValidation("Sec-WebSocket-Version", "13");
        request.Headers.TryAddWithoutValidation("Sec-WebSocket-Key", secWebSocketKey);

        if (Options.RequestedSubProtocols.Count > 0)
        {
            request.Headers.TryAddWithoutValidation("Sec-WebSocket-Protocol", string.Join(", ", Options.RequestedSubProtocols));
        }

        Options.ConfigureRequest?.Invoke(request);

        var response = await invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var upgradeHeader = GetHeaderValue(response, "Upgrade");
        var connectionHeader = GetHeaderValue(response, "Connection");
        var acceptHeader = GetHeaderValue(response, "Sec-WebSocket-Accept");
        var subProtocol = GetHeaderValue(response, "Sec-WebSocket-Protocol");

        ValidateResponse((HttpStatusCode)(int)response.StatusCode, upgradeHeader, connectionHeader, secWebSocketKey, acceptHeader);
        ValidateSubProtocol(subProtocol);

        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

        return WebSocket.CreateFromStream(stream, isServer: false, subProtocol, Options.KeepAliveInterval);
    }

    private static string? GetHeaderValue(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}
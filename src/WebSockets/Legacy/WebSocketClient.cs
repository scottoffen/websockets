using System.Net;
using System.Net.WebSockets;

namespace WebSockets;

public sealed partial class WebSocketClient
{
    static WebSocketClient()
    {
        // The same call ClientWebSocket's own static constructor makes; safe
        // to call more than once. This registers the ws:/wss: URI schemes with
        // WebRequest so that WebRequest.Create(wsUri) below returns a
        // specially-flagged HttpWebRequest that performs the native
        // WinHTTP-backed WebSocket upgrade instead of a normal HTTP request.
#pragma warning disable 618 // "This API supports the .NET Framework infrastructure" obsolete warning.
        WebSocket.RegisterPrefixes();
#pragma warning restore 618
    }

    private async partial Task<WebSocket> ConnectAsyncCore(Uri uri, CancellationToken cancellationToken)
    {
        var request = (HttpWebRequest)WebRequest.Create(uri);

        if (Options.RequestedSubProtocols.Count > 0)
        {
            request.Headers["Sec-WebSocket-Protocol"] = string.Join(", ", Options.RequestedSubProtocols);
        }

        Options.ConfigureRequest?.Invoke(request);

        using (cancellationToken.Register(() => request.Abort()))
        {
            var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);

            var secWebSocketKey = request.Headers["Sec-WebSocket-Key"];

            ValidateResponse(
                response.StatusCode,
                response.Headers["Upgrade"],
                response.Headers["Connection"],
                secWebSocketKey,
                response.Headers["Sec-WebSocket-Accept"]);

            var subProtocol = response.Headers["Sec-WebSocket-Protocol"];
            ValidateSubProtocol(subProtocol);

            var buffer = WebSocket.CreateClientBuffer(Options.ReceiveBufferSize, Options.SendBufferSize);

            return WebSocket.CreateClientWebSocket(
                response.GetResponseStream(),
                subProtocol,
                Options.ReceiveBufferSize,
                Options.SendBufferSize,
                Options.KeepAliveInterval,
                useZeroMaskingKey: false,
                internalBuffer: buffer);
        }
    }
}
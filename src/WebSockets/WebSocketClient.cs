using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace WebSockets;

/// <summary>
/// A <c>ClientWebSocket</c> replacement that fixes its overly strict
/// <c>Connection</c> response-header validation (it fails a spec-legal
/// response like "Connection: Upgrade, Keep-Alive" with "The 'Connection'
/// header value 'Upgrade, Keep-Alive' is invalid.", confirmed on both .NET
/// Framework and modern .NET).
///
/// This shared partial holds the platform-agnostic core: the validation
/// logic itself, operating on plain primitives rather than any
/// platform-specific response type. Each platform implements
/// <see cref="ConnectAsyncCore"/> in its own Legacy/ or Modern/
/// partial, doing whatever request/response mechanics that platform needs,
/// then calls into this shared validation.
/// </summary>
public sealed partial class WebSocketClient : IWebSocketClient
{
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <summary>Configuration for this client. Set before calling <see cref="ConnectAsync"/>.</summary>
    public WebSocketClientOptions Options { get; } = new();

    /// <summary>
    /// Connects to <paramref name="uri"/> and returns the resulting <see cref="WebSocket"/>
    /// once the handshake completes and passes validation.
    /// </summary>
    public Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
        => ConnectAsyncCore(uri, cancellationToken);

    private partial Task<WebSocket> ConnectAsyncCore(Uri uri, CancellationToken cancellationToken);

    private void ValidateResponse(
        HttpStatusCode statusCode,
        string? upgradeHeader,
        string? connectionHeader,
        string secWebSocketKey,
        string? acceptHeader)
    {
        // Not a delegate, deliberately: unlike the three checks below, 101 is
        // the actual RFC 6455 mechanism by which the connection switches
        // protocols at all. There's no server response shape where relaxing
        // this check reveals a connection that was secretly fine, so making
        // it overridable could only ever replace this clear message with a
        // confusing one from deeper in the platform.
        if (statusCode != HttpStatusCode.SwitchingProtocols)
        {
            throw new WebSocketException(
                $"The server returned status code '{(int)statusCode}' when status code '101' was expected.");
        }

        if (!Options.IsValidUpgradeHeader(upgradeHeader))
        {
            throw new WebSocketException($"The 'Upgrade' header value '{upgradeHeader}' is invalid.");
        }

        if (!Options.IsValidConnectionHeader(connectionHeader))
        {
            throw new WebSocketException($"The 'Connection' header value '{connectionHeader}' is invalid.");
        }

        var expectedAccept = ComputeAcceptKey(secWebSocketKey);
        if (!Options.IsValidAcceptHeader(expectedAccept, acceptHeader))
        {
            throw new WebSocketException($"The 'Sec-WebSocket-Accept' header value '{acceptHeader}' is invalid.");
        }
    }

    private void ValidateSubProtocol(string? subProtocol)
    {
        if (string.IsNullOrEmpty(subProtocol))
        {
            return;
        }

        if (Options.RequestedSubProtocols.Count == 0)
        {
            throw new WebSocketException(
                $"The server responded with sub-protocol '{subProtocol}' but none was requested.");
        }

        bool requested = Options.RequestedSubProtocols.Any(
            p => string.Equals(p, subProtocol, StringComparison.OrdinalIgnoreCase));

        if (!requested)
        {
            throw new WebSocketException(
                $"The server responded with sub-protocol '{subProtocol}' which was not among the requested sub-protocols.");
        }
    }

    private static string ComputeAcceptKey(string secWebSocketKey)
    {
        using var sha1 = SHA1.Create();
        var bytes = Encoding.ASCII.GetBytes(secWebSocketKey + WebSocketGuid);
        return Convert.ToBase64String(sha1.ComputeHash(bytes));
    }
}
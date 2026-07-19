using System.Net.WebSockets;
using System.Text;

namespace WebSockets.FakeServer;

/// <summary>
/// Shared round-trip proof used across test projects: sends a single text
/// frame and asserts the same content comes back, confirming a
/// <see cref="WebSocket"/> handed back by a connect call is genuinely alive
/// and usable, not just nominally connected.
///
/// Uses the <see cref="ArraySegment{T}"/>-based <c>SendAsync</c>/<c>ReceiveAsync</c>
/// overloads rather than the newer <c>Memory&lt;byte&gt;</c>-based ones: those
/// are the only overloads available on .NET Framework's <see cref="WebSocket"/>,
/// and they're still present on modern .NET too, so this one helper works
/// unmodified from both the net462 and net5.0 test projects.
///
/// Asserts via a plain thrown exception rather than an assertion library.
/// This project is test infrastructure shared by both test
/// projects, not itself bound to whichever assertion library either of them
/// happens to use.
/// </summary>
public static class RoundTripEcho
{
    /// <summary>
    /// Sends "ping" as a single text frame on <paramref name="socket"/> and
    /// throws if the echoed response isn't exactly "ping" back.
    /// </summary>
    public static async Task AssertWorksAsync(WebSocket socket)
    {
        var sendBuffer = Encoding.UTF8.GetBytes("ping");
        await socket.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

        var receiveBuffer = new byte[1024];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
        var message = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);

        if (message != "ping")
        {
            throw new InvalidOperationException($"Expected echo 'ping' but received '{message}'.");
        }
    }
}
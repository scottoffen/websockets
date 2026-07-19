using System.Net.WebSockets;
using WebSockets.FakeServer;

namespace WebSockets.Legacy.Tests;

public class WebSocketClientConnectionHeaderTests
{
    [Fact]
    public async Task ConnectAsync_Succeeds_WhenConnectionHeaderIsWellFormed()
    {
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade");
        var client = new WebSocketClient();

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);

        await RoundTripEcho.AssertWorksAsync(socket);
    }

    [Fact]
    public async Task ConnectAsync_Succeeds_WhenConnectionHeaderCombinesUpgradeAndKeepAlive()
    {
        // This is the exact header value that throws
        // ClientWebSocketConnectionHeaderTests.ConnectAsync_Throws_WhenConnectionHeaderCombinesUpgradeAndKeepAlive
        // against stock ClientWebSocket. This test is the actual fix.
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade, Keep-Alive");
        var client = new WebSocketClient();

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);

        await RoundTripEcho.AssertWorksAsync(socket);
    }
}
using System.Net.WebSockets;
using WebSockets.FakeServer;

namespace WebSockets.Modern.Tests;

public class ClientWebSocketConnectionHeaderTests
{
    [Fact]
    public async Task ConnectAsync_Succeeds_WhenConnectionHeaderIsWellFormed()
    {
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade");
        using var socket = new ClientWebSocket();

        await socket.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);

        await RoundTripEcho.AssertWorksAsync(socket);
    }

    [Fact]
    public async Task ConnectAsync_Throws_WhenConnectionHeaderCombinesUpgradeAndKeepAlive()
    {
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade, Keep-Alive");
        using var socket = new ClientWebSocket();

        var exception = await Should.ThrowAsync<WebSocketException>(
            () => socket.ConnectAsync(server.Uri, CancellationToken.None));

        exception.Message.ShouldBe("The 'Connection' header value 'Upgrade, Keep-Alive' is invalid.");
    }
}
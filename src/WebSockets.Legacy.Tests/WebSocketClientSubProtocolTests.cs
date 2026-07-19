using System.Net.WebSockets;
using WebSockets.FakeServer;

namespace WebSockets.Legacy.Tests;

public class WebSocketClientSubProtocolTests
{
    [Fact]
    public async Task ConnectAsync_Succeeds_WhenServerReturnsARequestedSubProtocol()
    {
        var response = new FakeWebSocketServerResponse { SubProtocol = "chat" };
        await using var server = await FakeWebSocketServer.StartAsync(response);
        var client = new WebSocketClient();
        client.Options.RequestedSubProtocols.Add("chat");

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);
        await RoundTripEcho.AssertWorksAsync(socket);
    }

    [Fact]
    public async Task ConnectAsync_Throws_WhenServerReturnsASubProtocolThatWasNotRequested()
    {
        var response = new FakeWebSocketServerResponse { SubProtocol = "other" };
        await using var server = await FakeWebSocketServer.StartAsync(response);
        var client = new WebSocketClient();
        client.Options.RequestedSubProtocols.Add("chat");

        var exception = await Should.ThrowAsync<WebSocketException>(
            () => client.ConnectAsync(server.Uri, CancellationToken.None));

        exception.Message.ShouldBe("The server responded with sub-protocol 'other' which was not among the requested sub-protocols.");
    }

    [Fact]
    public async Task ConnectAsync_Throws_WhenServerReturnsASubProtocol_ButNoneWereRequested()
    {
        var response = new FakeWebSocketServerResponse { SubProtocol = "chat" };
        await using var server = await FakeWebSocketServer.StartAsync(response);
        var client = new WebSocketClient();

        var exception = await Should.ThrowAsync<WebSocketException>(
            () => client.ConnectAsync(server.Uri, CancellationToken.None));

        exception.Message.ShouldBe("The server responded with sub-protocol 'chat' but none was requested.");
    }
}
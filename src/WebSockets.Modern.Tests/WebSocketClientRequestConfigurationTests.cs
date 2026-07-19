using WebSockets.FakeServer;

namespace WebSockets.Modern.Tests;

public class WebSocketClientRequestConfigurationTests
{
    [Fact]
    public async Task ConnectAsync_Invokes_ConfigureRequest()
    {
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade");
        var client = new WebSocketClient();

        var wasInvoked = false;
        HttpRequestMessage? capturedRequest = null;
        client.Options.ConfigureRequest = request =>
        {
            wasInvoked = true;
            capturedRequest = request;
        };

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        wasInvoked.ShouldBeTrue();
        capturedRequest.ShouldNotBeNull();
        await RoundTripEcho.AssertWorksAsync(socket);
    }

    [Fact]
    public async Task ConnectAsync_Invokes_ConfigureHandler()
    {
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade");
        var client = new WebSocketClient();

        var wasInvoked = false;
        SocketsHttpHandler? capturedHandler = null;
        client.Options.ConfigureHandler = handler =>
        {
            wasInvoked = true;
            capturedHandler = handler;
        };

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        wasInvoked.ShouldBeTrue();
        capturedHandler.ShouldNotBeNull();
        await RoundTripEcho.AssertWorksAsync(socket);
    }
}
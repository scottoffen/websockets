using System.Net;
using WebSockets.FakeServer;

namespace WebSockets.Legacy.Tests;

public class WebSocketClientRequestConfigurationTests
{
    [Fact]
    public async Task ConnectAsync_Invokes_ConfigureRequest()
    {
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade");
        var client = new WebSocketClient();

        var wasInvoked = false;
        HttpWebRequest? capturedRequest = null;
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
}
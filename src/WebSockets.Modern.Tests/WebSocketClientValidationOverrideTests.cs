using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WebSockets.FakeServer;

namespace WebSockets.Modern.Tests;

public class WebSocketClientValidationOverrideTests
{
    [Fact]
    public async Task ConnectAsync_Throws_WhenUpgradeHeaderIsInvalid_ByDefault()
    {
        var response = new FakeWebSocketServerResponse { UpgradeHeaderValue = "not-websocket" };
        await using var server = await FakeWebSocketServer.StartAsync(response);
        var client = new WebSocketClient();

        var exception = await Should.ThrowAsync<WebSocketException>(
            () => client.ConnectAsync(server.Uri, CancellationToken.None));

        exception.Message.ShouldBe("The 'Upgrade' header value 'not-websocket' is invalid.");
    }

    [Fact]
    public async Task ConnectAsync_Succeeds_WhenUpgradeHeaderOverrideAccepts()
    {
        var response = new FakeWebSocketServerResponse { UpgradeHeaderValue = "not-websocket" };
        await using var server = await FakeWebSocketServer.StartAsync(response);
        var client = new WebSocketClient();
        client.Options.IsValidUpgradeHeader = _ => true;

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);
        await RoundTripEcho.AssertWorksAsync(socket);
    }

    [Fact]
    public async Task ConnectAsync_Throws_WhenAcceptHeaderIsInvalid_ByDefault()
    {
        var response = new FakeWebSocketServerResponse { AcceptHeaderOverride = "not-a-real-accept-value" };
        await using var server = await FakeWebSocketServer.StartAsync(response);
        var client = new WebSocketClient();

        await Should.ThrowAsync<WebSocketException>(
            () => client.ConnectAsync(server.Uri, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAsync_Succeeds_WhenAcceptHeaderOverrideAccepts()
    {
        var response = new FakeWebSocketServerResponse { AcceptHeaderOverride = "not-a-real-accept-value" };
        await using var server = await FakeWebSocketServer.StartAsync(response);
        var client = new WebSocketClient();
        client.Options.IsValidAcceptHeader = (_, _) => true;

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);
        await RoundTripEcho.AssertWorksAsync(socket);
    }

    [Fact]
    public async Task ConnectAsync_Throws_WhenStatusCodeIsNotSwitchingProtocols()
    {
        // Not an "override" test like the others in this class: IsValidStatusCode
        // doesn't exist. 101 is the RFC 6455 mechanism by which the connection
        // switches protocols at all, so this check is hardcoded rather than
        // overridable, see WebSocketClient.ValidateResponse for why.
        var response = new FakeWebSocketServerResponse { StatusCode = 200, StatusDescription = "OK" };
        await using var server = await FakeWebSocketServer.StartAsync(response);
        var client = new WebSocketClient();

        var exception = await Should.ThrowAsync<WebSocketException>(
            () => client.ConnectAsync(server.Uri, CancellationToken.None));

        exception.Message.ShouldBe("The server returned status code '200' when status code '101' was expected.");
    }

    [Fact]
    public async Task ConnectAsync_Throws_WhenConnectionHeaderOverrideRejectsAnOtherwiseValidHeader()
    {
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade");
        var client = new WebSocketClient();
        client.Options.IsValidConnectionHeader = _ => false;

        await Should.ThrowAsync<WebSocketException>(
            () => client.ConnectAsync(server.Uri, CancellationToken.None));
    }
}
using System.Net.WebSockets;
using WebSockets.FakeServer;

namespace WebSockets.Legacy.Tests;

public class WebSocketClientCertificateValidationTests
{
    [Fact]
    public async Task ConnectAsync_Throws_WhenNoCallbackIsSet_AndCertificateIsUntrusted()
    {
        var certificate = SelfSignedCertificateFactory.Shared;
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", certificate);
        var client = new WebSocketClient();

        await Should.ThrowAsync<Exception>(
            () => client.ConnectAsync(server.Uri, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAsync_Succeeds_WhenConfigureRequestAcceptsUntrustedCertificate()
    {
        var certificate = SelfSignedCertificateFactory.Shared;
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", certificate);
        var client = new WebSocketClient();
        client.Options.ConfigureRequest = request =>
            request.ServerCertificateValidationCallback = (_, _, _, _) => true;

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);
        await RoundTripEcho.AssertWorksAsync(socket);
    }

    // This is the faithful, combined replica of the actual RX7000 report: a
    // working certificate bypass does NOT rescue the connection from the
    // Connection-header bug on stock ClientWebSocket, since the two checks
    // happen at separate, independent stages of the handshake, but
    // WebSocketClient handles both together on net462.
    [Fact]
    public async Task ConnectAsync_Succeeds_WhenConnectionHeaderIsInvalid_WithWorkingCertificateBypass()
    {
        var certificate = SelfSignedCertificateFactory.Shared;
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade, Keep-Alive", certificate);
        var client = new WebSocketClient();
        client.Options.ConfigureRequest = request =>
            request.ServerCertificateValidationCallback = (_, _, _, _) => true;

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);
        await RoundTripEcho.AssertWorksAsync(socket);
    }
}
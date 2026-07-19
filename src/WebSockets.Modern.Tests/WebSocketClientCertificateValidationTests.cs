using System.Net.WebSockets;
using WebSockets.FakeServer;

namespace WebSockets.Modern.Tests;

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
    public async Task ConnectAsync_Succeeds_WhenConfigureHandlerAcceptsUntrustedCertificate()
    {
        var certificate = SelfSignedCertificateFactory.Shared;
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", certificate);
        var client = new WebSocketClient();
        client.Options.ConfigureHandler = handler =>
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);
        await RoundTripEcho.AssertWorksAsync(socket);
    }

    // This is the faithful, combined replica of the actual RX7000 report,
    // proven for WebSocketClient specifically on net5.0: a working
    // certificate bypass plus the exact bad Connection header value that
    // throws against vanilla ClientWebSocket (see
    // ClientWebSocketCertificateValidationTests.ConnectAsync_Throws_WhenConnectionHeaderIsInvalid_EvenWithWorkingCertificateBypass)
    // succeeds here.
    [Fact]
    public async Task ConnectAsync_Succeeds_WhenConnectionHeaderIsInvalid_WithWorkingCertificateBypass()
    {
        var certificate = SelfSignedCertificateFactory.Shared;
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade, Keep-Alive", certificate);
        var client = new WebSocketClient();
        client.Options.ConfigureHandler = handler =>
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        using var socket = await client.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);
        await RoundTripEcho.AssertWorksAsync(socket);
    }
}
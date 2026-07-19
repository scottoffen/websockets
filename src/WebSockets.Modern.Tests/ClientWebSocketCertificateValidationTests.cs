using System.Net.WebSockets;
using WebSockets.FakeServer;

namespace WebSockets.Modern.Tests;

public class ClientWebSocketCertificateValidationTests
{
    // The Connection header in most of these tests is deliberately well-formed
    // ("Upgrade"): they isolate the TLS/certificate-callback behavior from the
    // Connection-header bug covered by ClientWebSocketConnectionHeaderTests.

    [Fact]
    public async Task ConnectAsync_Throws_WhenNoCallbackIsSet_AndCertificateIsUntrusted()
    {
        var certificate = SelfSignedCertificateFactory.Shared;
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", certificate);

        using var socket = new ClientWebSocket();

        await Should.ThrowAsync<Exception>(
            () => socket.ConnectAsync(server.Uri, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAsync_Succeeds_WhenCallbackAcceptsUntrustedCertificate()
    {
        var certificate = SelfSignedCertificateFactory.Shared;
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", certificate);

        using var socket = new ClientWebSocket();
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        await socket.ConnectAsync(server.Uri, CancellationToken.None);

        socket.State.ShouldBe(WebSocketState.Open);
    }

    // This is the faithful, combined replica of the actual RX7000 report: a
    // working certificate bypass (the thing the WOOFware POC relied on) does
    // NOT rescue the connection from the Connection-header bug, since the two
    // checks happen at separate, independent stages of the handshake. This is
    // the test that most directly debunks "it works on .NET 5.0."
    [Fact]
    public async Task ConnectAsync_Throws_WhenConnectionHeaderIsInvalid_EvenWithWorkingCertificateBypass()
    {
        var certificate = SelfSignedCertificateFactory.Shared;
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade, Keep-Alive", certificate);

        using var socket = new ClientWebSocket();
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        var exception = await Should.ThrowAsync<WebSocketException>(
            () => socket.ConnectAsync(server.Uri, CancellationToken.None));

        exception.Message.ShouldBe("The 'Connection' header value 'Upgrade, Keep-Alive' is invalid.");
    }
}
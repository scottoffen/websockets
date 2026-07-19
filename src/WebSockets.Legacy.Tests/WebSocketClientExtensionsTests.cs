using System.Net;
using WebSockets;

namespace WebSockets.Legacy.Tests;

public class WebSocketClientExtensionsTests
{
    [Fact]
    public void WithSubProtocol_AddsSubProtocol_AndReturnsSameInstance()
    {
        var client = new WebSocketClient();

        var result = client.WithSubProtocol("chat");

        result.ShouldBeSameAs(client);
        client.Options.RequestedSubProtocols.ShouldContain("chat");
    }

    [Fact]
    public void WithKeepAliveInterval_SetsInterval_AndReturnsSameInstance()
    {
        var client = new WebSocketClient();
        var interval = TimeSpan.FromSeconds(15);

        var result = client.WithKeepAliveInterval(interval);

        result.ShouldBeSameAs(client);
        client.Options.KeepAliveInterval.ShouldBe(interval);
    }

    [Fact]
    public void WithReceiveBufferSize_SetsSize_AndReturnsSameInstance()
    {
        var client = new WebSocketClient();

        var result = client.WithReceiveBufferSize(4096);

        result.ShouldBeSameAs(client);
        client.Options.ReceiveBufferSize.ShouldBe(4096);
    }

    [Fact]
    public void WithSendBufferSize_SetsSize_AndReturnsSameInstance()
    {
        var client = new WebSocketClient();

        var result = client.WithSendBufferSize(4096);

        result.ShouldBeSameAs(client);
        client.Options.SendBufferSize.ShouldBe(4096);
    }

    [Fact]
    public void WithUpgradeHeaderValidator_SetsValidator_AndReturnsSameInstance()
    {
        var client = new WebSocketClient();
        Func<string?, bool> validator = _ => true;

        var result = client.WithUpgradeHeaderValidator(validator);

        result.ShouldBeSameAs(client);
        client.Options.IsValidUpgradeHeader.ShouldBeSameAs(validator);
    }

    [Fact]
    public void WithConnectionHeaderValidator_SetsValidator_AndReturnsSameInstance()
    {
        var client = new WebSocketClient();
        Func<string?, bool> validator = _ => true;

        var result = client.WithConnectionHeaderValidator(validator);

        result.ShouldBeSameAs(client);
        client.Options.IsValidConnectionHeader.ShouldBeSameAs(validator);
    }

    [Fact]
    public void WithAcceptHeaderValidator_SetsValidator_AndReturnsSameInstance()
    {
        var client = new WebSocketClient();
        Func<string, string?, bool> validator = (_, _) => true;

        var result = client.WithAcceptHeaderValidator(validator);

        result.ShouldBeSameAs(client);
        client.Options.IsValidAcceptHeader.ShouldBeSameAs(validator);
    }

    [Fact]
    public void WithConfigureRequest_SetsConfigureRequest_AndReturnsSameInstance()
    {
        var client = new WebSocketClient();
        Action<HttpWebRequest> configure = _ => { };

        var result = client.WithConfigureRequest(configure);

        result.ShouldBeSameAs(client);
        client.Options.ConfigureRequest.ShouldBeSameAs(configure);
    }
}
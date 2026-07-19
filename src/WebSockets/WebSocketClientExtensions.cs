namespace WebSockets;

/// <summary>
/// Fluent, chainable configuration extensions over <see cref="IWebSocketClient"/>.
/// These don't add anything to <see cref="WebSocketClient"/> itself, they're
/// thin wrappers over the existing <see cref="WebSocketClientOptions"/>
/// properties that return the client so calls can be chained, e.g.
/// <c>new WebSocketClient().WithSubProtocol("chat").WithKeepAliveInterval(...)</c>.
/// Targeting the interface rather than the concrete class means the chain
/// stays usable against a mocked <see cref="IWebSocketClient"/> too.
/// </summary>
public static partial class WebSocketClientExtensions
{
    /// <summary>Adds a sub-protocol to <see cref="WebSocketClientOptions.RequestedSubProtocols"/>.</summary>
    public static IWebSocketClient WithSubProtocol(this IWebSocketClient client, string subProtocol)
    {
        client.Options.RequestedSubProtocols.Add(subProtocol);
        return client;
    }

    /// <summary>Sets <see cref="WebSocketClientOptions.KeepAliveInterval"/>.</summary>
    public static IWebSocketClient WithKeepAliveInterval(this IWebSocketClient client, TimeSpan interval)
    {
        client.Options.KeepAliveInterval = interval;
        return client;
    }

    /// <summary>Sets <see cref="WebSocketClientOptions.ReceiveBufferSize"/>.</summary>
    public static IWebSocketClient WithReceiveBufferSize(this IWebSocketClient client, int size)
    {
        client.Options.ReceiveBufferSize = size;
        return client;
    }

    /// <summary>Sets <see cref="WebSocketClientOptions.SendBufferSize"/>.</summary>
    public static IWebSocketClient WithSendBufferSize(this IWebSocketClient client, int size)
    {
        client.Options.SendBufferSize = size;
        return client;
    }

    /// <summary>Overrides <see cref="WebSocketClientOptions.IsValidUpgradeHeader"/>.</summary>
    public static IWebSocketClient WithUpgradeHeaderValidator(this IWebSocketClient client, Func<string?, bool> validator)
    {
        client.Options.IsValidUpgradeHeader = validator;
        return client;
    }

    /// <summary>Overrides <see cref="WebSocketClientOptions.IsValidConnectionHeader"/>.</summary>
    public static IWebSocketClient WithConnectionHeaderValidator(this IWebSocketClient client, Func<string?, bool> validator)
    {
        client.Options.IsValidConnectionHeader = validator;
        return client;
    }

    /// <summary>Overrides <see cref="WebSocketClientOptions.IsValidAcceptHeader"/>.</summary>
    public static IWebSocketClient WithAcceptHeaderValidator(this IWebSocketClient client, Func<string, string?, bool> validator)
    {
        client.Options.IsValidAcceptHeader = validator;
        return client;
    }
}
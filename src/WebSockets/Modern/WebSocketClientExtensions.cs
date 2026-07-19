using System.Net.Http;

namespace WebSockets;

public static partial class WebSocketClientExtensions
{
    /// <summary>Sets <see cref="WebSocketClientOptions.ConfigureRequest"/> (net5.0: <see cref="Action{HttpRequestMessage}"/>).</summary>
    public static IWebSocketClient WithConfigureRequest(this IWebSocketClient client, Action<HttpRequestMessage> configure)
    {
        client.Options.ConfigureRequest = configure;
        return client;
    }

    /// <summary>Sets <see cref="WebSocketClientOptions.ConfigureHandler"/> (net5.0 only).</summary>
    public static IWebSocketClient WithConfigureHandler(this IWebSocketClient client, Action<SocketsHttpHandler> configure)
    {
        client.Options.ConfigureHandler = configure;
        return client;
    }
}
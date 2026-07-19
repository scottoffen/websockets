using System.Net;

namespace WebSockets;

public static partial class WebSocketClientExtensions
{
    /// <summary>Sets <see cref="WebSocketClientOptions.ConfigureRequest"/> (net462: <see cref="Action{HttpWebRequest}"/>).</summary>
    public static IWebSocketClient WithConfigureRequest(this IWebSocketClient client, Action<HttpWebRequest> configure)
    {
        client.Options.ConfigureRequest = configure;
        return client;
    }
}
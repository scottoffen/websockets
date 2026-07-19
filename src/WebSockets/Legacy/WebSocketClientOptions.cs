using System.Net;

namespace WebSockets;

public sealed partial class WebSocketClientOptions
{
    /// <summary>
    /// General-purpose escape hatch, invoked after the request is created and
    /// before it's sent. Use this for anything <see cref="HttpWebRequest"/>
    /// already exposes that isn't wrapped explicitly above: <see cref="HttpWebRequest.Credentials"/>,
    /// <see cref="HttpWebRequest.Proxy"/>, <see cref="HttpWebRequest.ClientCertificates"/>,
    /// custom headers, and so on.
    /// </summary>
    public Action<HttpWebRequest>? ConfigureRequest { get; set; }
}
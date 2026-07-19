---
sidebar_position: 2
title: WebSocketClient
---

`WebSocketClient` is a sealed class implementing `IWebSocketClient`, the drop-in replacement for `System.Net.WebSockets.ClientWebSocket` that this package exists to provide.

```csharp
using WebSockets;

var client = new WebSocketClient();
using var socket = await client.ConnectAsync(new Uri("wss://example.com/socket"));
```

`ConnectAsync` returns a real `System.Net.WebSockets.WebSocket`, the same type `ClientWebSocket.ConnectAsync` would have handed you. `WebSocketClient` doesn't reimplement WebSocket framing itself, it performs the handshake, validates the response (see [WebSocketClientOptions](./websocketoptions)), and then reuses the platform's own native WebSocket implementation for everything after that:

- On .NET Framework, it registers the `ws:`/`wss:` schemes the same way `ClientWebSocket`'s own static constructor does, then hands the resulting stream to `System.Net.WebSockets.WebSocket.CreateClientWebSocket`.
- On modern .NET, it performs the handshake over `HttpClient`/`SocketsHttpHandler` and hands the resulting stream to `System.Net.WebSockets.WebSocket.CreateFromStream`.

Once you have the `WebSocket` back, use it exactly as you would any other: `SendAsync`, `ReceiveAsync`, `CloseAsync`, all identical to `ClientWebSocket`. Nothing about `WebSocketClient`'s involvement continues past the initial connection, it's a fix for the handshake, not a new abstraction layered over the socket itself.

## Configuration

All configuration lives on the `Options` property, a [`WebSocketClientOptions`](./websocketoptions) instance, set before calling `ConnectAsync`:

```csharp
var client = new WebSocketClient();
client.Options.RequestedSubProtocols.Add("chat");

using var socket = await client.ConnectAsync(uri);
```

Every property on `Options` also has a corresponding fluent extension method, so configuration doesn't have to break out of a chain, see [Fluent Extensions](./fluent-extensions).

## Why a sealed class and a separate interface

`WebSocketClient` is `sealed`, nothing about this library is meant to be extended via inheritance. For unit testing, inject `IWebSocketClient` wherever you'd otherwise take a concrete `WebSocketClient`, and mock the interface freely:

```csharp
public interface IWebSocketClient
{
    WebSocketClientOptions Options { get; }
    Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken = default);
}
```

This is the same pattern commonly used around `HttpClient` for the same reason: a concrete, sealed implementation for real use, plus a narrow interface purely for substitutability in tests.

## Targeting

`WebSocketClient` targets `net462` and `net5.0`. The actual mechanics of the handshake (`HttpWebRequest` on .NET Framework vs. `HttpClient`/`SocketsHttpHandler` on modern .NET) differ between the two, which is why some options on `WebSocketClientOptions` (`ConfigureRequest`, `ConfigureHandler`) differ by platform, see [WebSocketClientOptions](./websocketoptions) for details.
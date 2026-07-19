---
sidebar_position: 6
title: Fluent Extensions
---

Every configurable property across both packages has a corresponding chainable extension method. These don't add any new behavior, they're thin wrappers that set the same underlying properties and return the client, so configuration doesn't have to break out of a fluent chain. All of them target the relevant interface (`IWebSocketClient` / `IResilientWebSocketClient`) rather than the concrete class, so a fluent chain stays usable against a mocked client in tests too.

## WebSockets

Extensions over [`WebSocketClientOptions`](./websocketoptions).

### Shared (both platforms)

| Method | Sets |
|---|---|
| `WithSubProtocol(string subProtocol)` | Adds to `RequestedSubProtocols` |
| `WithKeepAliveInterval(TimeSpan interval)` | `KeepAliveInterval` |
| `WithReceiveBufferSize(int size)` | `ReceiveBufferSize` |
| `WithSendBufferSize(int size)` | `SendBufferSize` |
| `WithUpgradeHeaderValidator(Func<string?, bool> validator)` | `IsValidUpgradeHeader` |
| `WithConnectionHeaderValidator(Func<string?, bool> validator)` | `IsValidConnectionHeader` |
| `WithAcceptHeaderValidator(Func<string, string?, bool> validator)` | `IsValidAcceptHeader` |

```csharp
var client = new WebSocketClient()
    .WithSubProtocol("chat")
    .WithUpgradeHeaderValidator(value =>
        string.Equals(value, "websocket", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "WebSocket", StringComparison.Ordinal)); // some quirky server
```

### net462

| Method | Sets |
|---|---|
| `WithConfigureRequest(Action<HttpWebRequest> configure)` | `ConfigureRequest` |

```csharp
var client = new WebSocketClient()
    .WithConfigureRequest(request =>
        request.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true);
```

### net5.0

| Method | Sets |
|---|---|
| `WithConfigureRequest(Action<HttpRequestMessage> configure)` | `ConfigureRequest` |
| `WithConfigureHandler(Action<SocketsHttpHandler> configure)` | `ConfigureHandler` |

```csharp
var client = new WebSocketClient()
    .WithConfigureHandler(handler =>
        handler.SslOptions.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true);
```

There's no `WithSendQueueCapacity`-style gap to worry about here; every property on `WebSocketClientOptions` is read at connection time, not just once at construction, so setting any of these, even immediately before calling `ConnectAsync`, always takes effect.

## WebSockets.Resilience

Extensions over [`ResilientWebSocketClientOptions`](./resilienceoptions). These aren't split by platform, `ResilientWebSocketClient` and its options are identical across `net462` and `net5.0`.

| Method | Sets |
|---|---|
| `WithBackoffStrategy(BackoffStrategy strategy)` | `BackoffStrategy` |
| `WithInitialReconnectDelay(TimeSpan delay)` | `InitialReconnectDelay` |
| `WithMaxReconnectDelay(TimeSpan delay)` | `MaxReconnectDelay` |

Since `AsResilient` and these extensions all return the client, and `StartAsync`/`SendAsync` on `IResilientWebSocketClient` do too, the whole thing chains end to end, including configuring the wrapped `IWebSocketClient` first:

```csharp
var resilient = await new WebSocketClient()
    .WithSubProtocol("chat")
    .AsResilient(uri)
    .WithBackoffStrategy(BackoffStrategy.Exponential)
    .WithMaxReconnectDelay(TimeSpan.FromMinutes(1))
    .StartAsync();

await resilient.SendAsync("hello");
```

### Why there's no `WithSendQueueCapacity`

Unlike the three properties above, which are read fresh on every reconnect attempt, `SendQueueCapacity` is only ever read once, at construction, to size the internal send queue. A fluent extension callable on an already-constructed `IResilientWebSocketClient` would compile fine, appear to work, and silently do nothing, worse than not having it at all. Set `SendQueueCapacity` via the `AsResilient(uri, Action<ResilientWebSocketClientOptions>)` overload instead, which runs before the client is built, see [ResilientWebSocketClientOptions](./resilienceoptions).
---
sidebar_position: 4
title: ResilientWebSocketClient
---

`ResilientWebSocketClient` layers reconnection, a configurable backoff strategy, a send queue that survives reconnect gaps, and fragmented-message reassembly on top of [`IWebSocketClient`](./websocket).

A live `WebSocket` (from `ClientWebSocket`, or `WebSockets`' own `WebSocketClient`) is a single, one-shot connection by design, it can't reconnect itself. `ResilientWebSocketClient` can't just hand one back the way `WebSocketClient.ConnectAsync` does, so it's a genuinely different contract: it owns the reconnect loop itself and gives the caller a stable send/receive surface across however many underlying connections happen along the way. That's why this lives in a separate package rather than as an option on `WebSocketClient`.

## Getting started

The entry point is the `AsResilient` extension method on `IWebSocketClient`:

```csharp
using WebSockets;
using WebSockets.Resilience;

var client = new WebSocketClient();
var resilient = await client.AsResilient(new Uri("wss://example.com/socket")).StartAsync();

resilient.MessageReceived += message => Console.WriteLine(message);

await resilient.SendAsync("hello");
```

`ResilientWebSocketClient` doesn't construct or configure the wrapped `IWebSocketClient` itself, it's handed one and calls `ConnectAsync` on it for every connection attempt, initial and every subsequent reconnect. Configure the wrapped client's `.Options` (headers, sub-protocols, TLS callbacks, etc.) *before* handing it over:

```csharp
var client = new WebSocketClient();
client.Options.RequestedSubProtocols.Add("chat");

var resilient = await client.AsResilient(uri).StartAsync();
```

Whatever's configured on the wrapped client applies uniformly across every connection `ResilientWebSocketClient` makes; there's no separate configuration surface on the resilient client itself for this.

See `AsResilient`'s two overloads and configuring backoff/timeouts in [ResilientWebSocketClientOptions](./resilienceoptions).

## `IResilientWebSocketClient`

Like `WebSocketClient`/`IWebSocketClient`, `ResilientWebSocketClient` is `sealed` and implements an interface, `IResilientWebSocketClient`, purely for mocking in unit tests.

```csharp
public interface IResilientWebSocketClient : IAsyncDisposable
{
    ResilientWebSocketClientOptions Options { get; }

    event Action<Exception>? ReconnectFailed;
    event Action? Reconnected;
    event Action<string>? MessageReceived;

    Task<IResilientWebSocketClient> StartAsync(CancellationToken cancellationToken = default);
    Task<IResilientWebSocketClient> SendAsync(string message, CancellationToken cancellationToken = default);
    Task CloseAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
```

`StartAsync` and `SendAsync` both return `IResilientWebSocketClient` (the same instance), so configuration, starting, and sending all chain together, see [Fluent Extensions](./fluent-extensions).

## Events

```csharp
resilient.Reconnected += () =>
{
    // A new connection has no memory of the previous session; resubscribe
    // to whatever channels/topics/state the server needs re-established.
    // Does not fire for the initial connection made by StartAsync, only
    // for connections after a reconnect.
};

resilient.ReconnectFailed += ex =>
{
    // A reconnect attempt failed. The client keeps retrying regardless;
    // this is purely observational.
};

resilient.MessageReceived += message =>
{
    // Raised once per fully-reassembled incoming text message, even if the
    // server sent it fragmented across multiple WebSocket frames.
};
```

## Shutting down: `DisposeAsync` vs. `CloseAsync`

`DisposeAsync` stays fast and abrupt, matching `WebSocket.Dispose()`'s own convention: it tears down the connection and reconnect loop immediately, without waiting for anything still queued to actually go out. Any message sitting in the send queue at that moment can be lost.

`CloseAsync(TimeSpan timeout, CancellationToken cancellationToken = default)` is the opt-in graceful path, the same relationship `WebSocket.CloseAsync` has to `WebSocket.Dispose()`. It stops accepting new sends immediately, waits up to `timeout` for anything already queued to actually be sent (including giving a reconnect a chance to happen first, if the client is disconnected when you call it), then tears everything down, all as one atomic operation:

```csharp
await resilient.CloseAsync(TimeSpan.FromSeconds(5));
```

Calling `DisposeAsync` afterward (e.g. via `await using`) is safe and a no-op; the teardown only actually runs once, however it's triggered.

## What this does and doesn't guarantee

- **A dropped connection is detected and reconnected automatically**, with the backoff strategy you choose.
- **A message sent while genuinely disconnected is queued and delivered once reconnected.** This is a real guarantee: `SendAsync` never throws just because there's no live connection at the moment.
- **A message that fails to send is requeued** rather than silently dropped, so a connection dying mid-send doesn't lose it.
- **Shutting down doesn't have to lose queued messages**, if you use `CloseAsync` instead of `DisposeAsync`.

**What isn't, and can't be, fully guaranteed**: a message sent in the exact instant a connection is dying, before either side has noticed yet. TCP doesn't guarantee a local send fails just because the remote end already reset the connection; a write can appear to succeed at the socket-buffer level into a connection that's already dead, with no exception raised. Closing that gap completely needs an application-level acknowledgment protocol (the server confirms receipt, the client only clears its own queue on that confirmation), which requires server-side cooperation and is out of scope for a general-purpose client.

For what it's worth, this is the same limitation the popular [`Websocket.Client`](https://github.com/Marfusios/websocket-client) library has, based on its public source: it also queues sends without waiting for delivery confirmation. Its handling of an actual send failure appears to log and drop the message rather than requeue it; `ResilientWebSocketClient` requeues on a failed send, which narrows, but does not eliminate, the gap above.
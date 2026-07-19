---
sidebar_position: 7
title: FakeWebSocketServer
---

`FakeWebSocketServer` is the test double both `WebSockets.Legacy.Tests` and `WebSockets.Modern.Tests` use to reproduce handshake behavior deterministically, without depending on a real external WebSocket server. It lives in `WebSockets.FakeServer`, which is not published as a NuGet package, it's internal test infrastructure only, referenced via `ProjectReference` from the two test projects.

:::info

This page is for contributors working on this repo, not for consumers of the published packages, if you're looking for how to use `WebSocketClient` or `ResilientWebSocketClient`, see the rest of this site instead.

:::

## Basic usage

```csharp
await using var server = await FakeWebSocketServer.StartAsync("Upgrade");

var client = new WebSocketClient();
using var socket = await client.ConnectAsync(server.Uri);
```

By default, the server:

- Binds an OS-assigned loopback port (never a fixed one, so tests never collide).
- Accepts exactly one connection.
- Sends back a correct, well-formed handshake response, except for whatever `Connection` header value you specify.
- Echoes back the first text frame it receives, then the connection ends.
- Tears itself down automatically after a 5-second safety-net timeout, in case a broken test would otherwise hang the suite.

Everything below is about deviating from that default in a controlled way.

## Controlling the handshake response: `FakeWebSocketServerResponse`

For anything beyond "wrong `Connection` header," use the full-response overload:

```csharp
var response = new FakeWebSocketServerResponse
{
    StatusCode = 200,
    StatusDescription = "OK",
    UpgradeHeaderValue = "not-websocket",
    ConnectionHeaderValue = "Upgrade",
    SubProtocol = "chat",
    AcceptHeaderOverride = "not-a-real-accept-value",
};

await using var server = await FakeWebSocketServer.StartAsync(response);
```

Every property defaults to a correct, spec-compliant value, so you only need to set the one thing you're deliberately breaking. `AcceptHeaderOverride` sends that value verbatim instead of the correctly-computed one; leave it `null` (the default) to have the server compute it properly.

## Controlling connection lifecycle: `FakeWebSocketServerConnectionOptions`

Passed as a separate parameter, since this is a different concern from what the handshake response looks like:

```csharp
var connectionOptions = new FakeWebSocketServerConnectionOptions
{
    SupportsReconnection = true,
    FailFirstConnections = 1,
    FragmentEchoIntoChunksOfSize = 4,
};

await using var server = await FakeWebSocketServer.StartAsync("Upgrade", connectionOptions: connectionOptions);
```

### `SupportsReconnection`

Default `false`: the original single-connection, single-echo behavior. Set `true` for anything resilience-related, it changes two things at once:

- The server keeps accepting new connections after each one ends, instead of stopping after the first.
- Each connection runs a continuous echo loop (read a frame, echo it, repeat) rather than handling exactly one message.

### `FailFirstConnections`

Simulates a server that's briefly unreachable during a reconnect. The initial connection (connection #1) is never affected by this, regardless of the value; it applies to the `N` reconnect attempts immediately following it, each one accepted at the TCP level (so it still counts toward `ConnectionCount`) but abandoned before ever reading the handshake request. Only meaningful alongside `SupportsReconnection: true`, there's no accept loop for a later attempt to succeed in otherwise.

### `FragmentEchoIntoChunksOfSize`

Splits the echoed response across multiple real WebSocket frames of at most this many bytes each (first frame `text`/`FIN=0`, middle frames `continuation`/`FIN=0`, last frame `continuation`/`FIN=1`), instead of always sending it as one complete frame. Use this to exercise a client's fragmented-message reassembly logic. `null` (the default) preserves the original single-frame behavior.

Note this only affects outgoing (echoed) frames. The server's own *reading* of incoming frames (`ReadTextFrameAsync`) doesn't handle fragmented input, it assumes the client always sends a single, complete frame, which is true of every client in this repo's own test suite.

## TLS

Pass a certificate to have the server speak `wss://` instead of `ws://`:

```csharp
using var certificate = SelfSignedCertificateFactory.Shared;
await using var server = await FakeWebSocketServer.StartAsync("Upgrade", certificate);
```

`SelfSignedCertificateFactory` has two members:

- **`Shared`**: a single certificate generated once and reused for the process's lifetime. Use this for almost everything, generating and importing a fresh certificate per test is slower and was the actual cause of real, intermittent test failures under xUnit's default parallel execution (concurrent imports contending over the OS-level key store). Callers must not dispose this instance.
- **`Create(string subjectName = "CN=localhost")`**: generates a genuinely fresh, distinct certificate. Only use this if a test specifically needs a certificate guaranteed distinct from others; callers of this method own the returned instance and are responsible for disposing it.

TLS is authenticated entirely separately from, and prior to, the handshake-response logic above; a `FakeWebSocketServerResponse` and `FakeWebSocketServerConnectionOptions` both still apply normally on top of TLS.

## Simulating a dropped connection: `DropCurrentConnectionAsync`

```csharp
await server.DropCurrentConnectionAsync();
```

Forcibly, abruptly terminates the current connection, an RST rather than a graceful close (via `LingerState = new LingerOption(true, 0)` before closing), so the client sees a genuine connection reset rather than a clean EOF. This is what makes reconnection tests realistic: a real outage doesn't politely send a WebSocket close frame first.

Only meaningful with `SupportsReconnection: true`; after the drop, the accept loop goes back to waiting for a new connection. No-op if nothing is currently connected.

## Observing what happened: `ConnectionCount` and `LastMessageReceived`

```csharp
server.ConnectionCount // int, how many connections have been accepted so far
server.LastMessageReceived // string?, the most recent message read from a client
```

`ConnectionCount` increments on every accepted connection, including ones immediately aborted via `FailFirstConnections`, so a test can assert exactly how many attempts happened (e.g., "went from 1 to 2 after a drop," or "reached 3 after one failed reconnect and one successful one").

`LastMessageReceived` is set the moment the server reads a message, independent of whether it's been echoed back yet, or whether the client is even still around to receive that echo. This matters specifically for testing things like `ResilientWebSocketClient.CloseAsync`, which only guarantees a queued message gets *sent*, not that the client's own receive loop survives long enough to process the echoed response; asserting on `LastMessageReceived` verifies the send actually reached the server without depending on that separate, unguaranteed timing.

## Timeouts

```csharp
await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20));
```

Every `StartAsync` overload takes a `timeout` (default 5 seconds), a safety net that tears the whole server down if the accept/handshake/echo sequence hasn't completed by then, so a bug in whatever's under test fails the test quickly instead of hanging the suite.

The default is fine for simple, single-shot tests. Bump it for anything involving `SupportsReconnection`: the default races against realistic reconnect timing (a drop, a reconnect attempt, maybe a deliberate failure via `FailFirstConnections`, all needing to happen within the window), and a test that happens to run slower under load (exactly what xUnit's parallel test execution introduces) can trip the server's own internal timeout mid-test, which looks identical to a real connection failure but isn't one. Every resilience test in this repo passes `timeout: TimeSpan.FromSeconds(20)` for this reason.

## Common scenarios

A quick reference for which knobs combine for which kind of test:

| Scenario | How |
|---|---|
| Reproduce the `Connection` header bug | `StartAsync("Upgrade, Keep-Alive")` |
| A specific handshake header is malformed | `FakeWebSocketServerResponse` with that one property set |
| Certificate/TLS bypass | `serverCertificate: SelfSignedCertificateFactory.Shared` |
| Sub-protocol negotiation | `FakeWebSocketServerResponse.SubProtocol` |
| Reconnect after a drop | `SupportsReconnection: true` + `DropCurrentConnectionAsync()` |
| A reconnect attempt fails, then recovers | `SupportsReconnection: true` + `FailFirstConnections` |
| Fragmented-message reassembly | `SupportsReconnection: true` + `FragmentEchoIntoChunksOfSize` |
| Verify a send actually reached the server, independent of the client's receive loop | `server.LastMessageReceived` |

## `RoundTripEcho`

A small shared assertion helper, also in `WebSockets.FakeServer`, used across both test projects to avoid duplicating the same send/receive/assert logic:

```csharp
await RoundTripEcho.AssertWorksAsync(socket);
```

Sends `"ping"` as a single text frame and throws if the echoed response isn't exactly `"ping"` back. Deliberately uses the `ArraySegment<byte>`-based `SendAsync`/`ReceiveAsync` overloads rather than the newer `Memory<byte>`-based ones, since those are the only overloads available on .NET Framework's `WebSocket`, and they're still present on modern .NET too, so this one helper works unmodified from both test projects.

## Known limitations

- **Single client at a time.** There's no support for multiple concurrent connections to the same server instance.
- **No fragmented *incoming* message support.** The server can send fragmented frames (`FragmentEchoIntoChunksOfSize`) but assumes any frame it reads from a client is complete and unfragmented.
- **No per-connection response variation.** The same `FakeWebSocketServerResponse` applies to every connection for the lifetime of a `SupportsReconnection: true` server; there's no way to say "fail the handshake on attempt 2, succeed on attempt 3."
- **Not thread-safe for multiple simultaneous test-driven operations** beyond what's documented above (e.g., calling `DropCurrentConnectionAsync` concurrently with the server naturally cycling to a new connection has the usual small race window inherent to any such test double; the existing tests account for this by waiting on observable signals like events or `ConnectionCount`/`LastMessageReceived` rather than fixed delays).
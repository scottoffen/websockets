---
sidebar_position: 3
title: WebSocketClientOptions
---

All configuration for [`WebSocketClient`](./websocket) lives on `Options`, a `WebSocketClientOptions` instance. Set these before calling `ConnectAsync`.

| Property | Type | Platform | Default |
|---|---|---|---|
| `RequestedSubProtocols` | `ICollection<string>` | both | empty |
| `KeepAliveInterval` | `TimeSpan` | both | `WebSocket.DefaultKeepAliveInterval` (30s) |
| `ReceiveBufferSize` | `int` | both | `16384` |
| `SendBufferSize` | `int` | both | `16384` |
| `IsValidUpgradeHeader` | `Func<string?, bool>` | both | requires `"websocket"` (case-insensitive) |
| `IsValidConnectionHeader` | `Func<string?, bool>` | both | accepts if `"Upgrade"` is any comma-separated token |
| `IsValidAcceptHeader` | `Func<string, string?, bool>` | both | exact match (case-insensitive) against the computed accept key |
| `ConfigureRequest` | `Action<HttpWebRequest>?` | net462 | `null` |
| `ConfigureRequest` | `Action<HttpRequestMessage>?` | net5.0 | `null` |
| `ConfigureHandler` | `Action<SocketsHttpHandler>?` | net5.0 | `null` |

## Sub-protocols

```csharp
client.Options.RequestedSubProtocols.Add("chat");
```

If the server responds with a sub-protocol not in this collection, `ConnectAsync` throws. This negotiation check itself isn't overridable, only which values you request is.

## Validation delegates

These three are the actual reason this package exists: `ClientWebSocket` hardcodes its handshake-response validation internally, with no way to relax any of it. `WebSocketClient` exposes each check as a delegate instead, with defaults that are RFC 6455-compliant, and, for `Connection`, deliberately more lenient than `ClientWebSocket`'s own check.

### `IsValidUpgradeHeader`

Validates the `Upgrade` response header. Default requires it equal `"websocket"`, case-insensitive.

### `IsValidConnectionHeader`

Validates the `Connection` response header. **This default is the actual fix.** `ClientWebSocket` requires an exact match against `"Upgrade"`; `WebSocketClient`'s default instead treats the header as a comma-separated token list and accepts it as long as `"Upgrade"` is one of the tokens. A server responding with `Connection: Upgrade, Keep-Alive`, spec-legal, but rejected by `ClientWebSocket`, connects successfully here by default.

Override only if you need to relax or tighten this further, for example to tolerate a non-standard value from a specific service:

```csharp
client.Options.IsValidConnectionHeader = value =>
    (value ?? "").Split(',').Select(t => t.Trim())
        .Any(t => string.Equals(t, "Upgrade", StringComparison.OrdinalIgnoreCase));
```

### `IsValidAcceptHeader`

Validates the `Sec-WebSocket-Accept` response header against the value computed from the request's `Sec-WebSocket-Key`. Default is an exact, case-insensitive match, per RFC 6455.

### Why there's no `IsValidStatusCode`

Unlike the three delegates above, there's no override for the response status code, it's hardcoded to require `101 Switching Protocols`. This is deliberate, not an oversight: per RFC 6455 §4.1, `101` is the actual mechanism by which the connection switches protocols at all. If a server never sends it, the connection never leaves ordinary HTTP semantics, there's no server response shape where relaxing this check would reveal a connection that was secretly fine. Overriding it could only ever suppress `WebSocketClient`'s own clear exception in favor of a confusing one from deeper in the platform (on .NET Framework, `WebSocket.CreateClientWebSocket` itself rejects a non-upgraded stream as non-writable), it can't retroactively cause a protocol switch that never happened on the wire.

## Certificate validation

For connecting to servers presenting self-signed or otherwise untrusted certificates, use `ConfigureRequest` (net462) or `ConfigureHandler` (net5.0):

**.NET Framework (net462):**

```csharp
client.Options.ConfigureRequest = request =>
    request.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;
```

**Modern .NET (net5.0):**

```csharp
client.Options.ConfigureHandler = handler =>
    handler.SslOptions.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;
```

## Other request/connection configuration

`ConfigureRequest` and `ConfigureHandler` are general-purpose escape hatches, invoked just before the request is sent, for anything not wrapped explicitly above: custom headers, credentials, proxy configuration, client certificates, and so on.

- **net462**: `ConfigureRequest` takes an `Action<HttpWebRequest>`, request and connection-level settings live on the same object.
- **net5.0**: `ConfigureRequest` takes an `Action<HttpRequestMessage>` (request-level settings, e.g. custom headers); `ConfigureHandler` takes an `Action<SocketsHttpHandler>` (connection-level settings, e.g. proxy, credentials, TLS options). These are separate because modern .NET's networking stack separates connection-level configuration from the request message itself, unlike `HttpWebRequest`, which bundles both.
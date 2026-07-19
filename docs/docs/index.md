---
sidebar_position: 1
title: Introduction
---

# WebSockets

WebSockets is a `ClientWebSocket` replacement for .NET Framework and modern .NET that fixes a specific, durable handshake-validation bug, and supplies an optional resilient layer built on top of it.

`System.Net.WebSockets.ClientWebSocket` validates its handshake response more strictly than the [WebSocket protocol specification (RFC 6455)](https://www.rfc-editor.org/rfc/rfc6455) requires. Its `Connection` header check demands an exact match against `"Upgrade"`, so a spec-legal response like `Connection: Upgrade, Keep-Alive` fails with:

```
System.Net.WebSockets.WebSocketException: The 'Connection' header value 'Upgrade, Keep-Alive' is invalid.
```

This happens on both .NET Framework and modern .NET, the check is hardcoded on both platforms, with no option to relax it and no way to work around it from calling code. You can see this demonstrated directly against a real `ClientWebSocket` in our test suite, on [.NET Framework](https://github.com/scottoffen/websockets/blob/main/src/WebSockets.Legacy.Tests/ClientWebSocketConnectionHeaderTests.cs) and on [modern .NET](https://github.com/scottoffen/websockets/blob/main/src/WebSockets.Modern.Tests/ClientWebSocketConnectionHeaderTests.cs).

This site documents two packages built around that problem.

:::info

Is `WebSockets` still maintained?

It can be easy to assume that no activity on a repository and/or no updates in a long time means that a project has been abandoned. That is absolutely not the case here!

The reality is that this package is designed to fix an issue that isn't going to change, so we might go long stretches between updates, if there's ever another one needed at all. That's driven by a few things:
- .NET Framework 4.6.2, while still supported by Microsoft as part of Windows, is a stable, feature-frozen target that Microsoft's ongoing .NET investment has moved past.
- .NET 5.0 is out of official support - but we chose that target so that we could provide support for all future .NET versions.
- Microsoft treats this as intended behavior rather than a defect to patch, so there's no reason to expect it to disappear on its own in some future .NET release.
- Given that nothing here is expected to change, there's nothing that would obviously require this package to change either.

You can rest assured that this package will continue to be maintained **if** any issues are ever reported or new features requested.

:::

## WebSockets

A `ClientWebSocket` replacement that fixes the bug above by treating handshake validation as overridable rather than hardcoded. A server responding with `Connection: Upgrade, Keep-Alive` connects successfully by default, no configuration required.

## WebSockets.Resilience

Reconnection, configurable backoff, and a send queue that survives reconnect gaps, layered on top of `WebSockets`. A live `WebSocket` is a single, one-shot connection by design; this adds everything a real connection needs around that limitation.

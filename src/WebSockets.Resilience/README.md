# WebSockets.Resilience

Targets `net462` and `net5.0`. Full documentation is at https://scottoffen.github.io/websockets.

## Overview

`ResilientWebSocketClient` layers reconnection, a configurable backoff strategy, a send queue that survives reconnect gaps, and fragmented-message reassembly on top of `IWebSocketClient`

A live `WebSocket` (from `ClientWebSocket` or `WebSockets`' own `WebSocketClient`) is a single, one-shot connection by design, it can't reconnect itself. `ResilientWebSocketClient` can't just hand one back the way `WebSocketClient.ConnectAsync` does; it owns the reconnect loop itself and gives the caller a stable send/receive surface across however many underlying connections happen along the way.

## Using this package guarantees

Using this package means:
- **A dropped connection is detected and reconnected automatically**, with the backoff strategy you choose.
- **A message sent while genuinely disconnected is queued and delivered once reconnected.** This is a real guarantee: `SendAsync` never throws just because there's no live connection at the moment.
- **A message that fails to send is requeued** rather than silently dropped, so a connection dying mid-send doesn't lose it.
- **Shutting down doesn't have to lose queued messages.** `DisposeAsync` stays fast and abrupt, and can lose whatever's still queued; `CloseAsync` instead gives everything already queued a bounded chance to actually go out first.

## What can't be guaranteed

A message sent in the exact instant a connection is dying, before either side has noticed yet. TCP doesn't guarantee a local send fails just because the remote end already reset the connection; a write can appear to succeed at the socket-buffer level into a connection that's already dead, with no exception raised.

Closing that gap completely needs an application-level acknowledgment protocol (the server confirms receipt, the client only clears its own queue on that confirmation), which requires server-side cooperation and is out of scope for a general-purpose client.

`ResilientWebSocketClient` requeues on a failed send, which narrows, but does not eliminate, the gap above.
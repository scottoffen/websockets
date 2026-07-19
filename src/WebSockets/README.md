# WebSockets

Targets `net462` and `net5.0`. Full documentation is at https://scottoffen.github.io/websockets.

> **Is `WebSockets` still maintained?**
>
> It can be easy to assume that no activity on a repository and/or no updates in a long time means that a project has been abandoned. That is absolutely not the case here!
>
> The reality is that this package is designed to fix an issue that isn't going to change, so we might go long stretches between updates, if there's ever another one needed at all. That's driven by a few things:
> - .NET Framework 4.6.2, while still supported by Microsoft as part of Windows, is a stable, feature-frozen target that Microsoft's ongoing .NET investment has moved past.
> - .NET 5.0 is out of official support - but we chose that target so that we could provide support for all future .NET versions.
> - Microsoft treats this as intended behavior rather than a defect to patch, so there's no reason to expect it to disappear on its own in some future .NET release.
> - Given that nothing here is expected to change, there's nothing that would obviously require this package to change either.
>
> You can rest assured that this package will continue to be maintained **if** any issues are ever reported or new features requested.

## Overview

`WebSocketClient` is a [`System.Net.WebSockets.ClientWebSocket`](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket) replacement that performs the same handshake and reuses the same underlying native WebSocket implementation for framing, but treats `Upgrade`, `Connection`, and `Sec-WebSocket-Accept` header validation as overridable delegates with sensible, [RFC 6455](https://www.rfc-editor.org/rfc/rfc6455)-compliant defaults, rather than hardcoding them internally the way `ClientWebSocket` does.

The default `Connection` check is the fix this package exists for. `ClientWebSocket` validates the handshake response more strictly than the spec requires: its `Connection` header check demands an exact match against `"Upgrade"`, so a spec-legal response like `Connection: Upgrade, Keep-Alive` fails with:

```
System.Net.WebSockets.WebSocketException: The 'Connection' header value 'Upgrade, Keep-Alive' is invalid.
```

This affects both .NET Framework and modern .NET. The check is hardcoded on both platforms, with no option to relax it and no way to work around it from calling code. `WebSocketClient`'s default `Connection` check instead treats the header as a comma-separated token list and accepts it as long as `Upgrade` is one of the tokens, rather than requiring an exact match against the whole value. A server responding with `Connection: Upgrade, Keep-Alive` connects successfully by default, no configuration required.

Need reconnection, backoff, or a send queue that survives a dropped connection? Check out [WebSockets.Resilience](https://www.nuget.org/packages/WebSockets.Resilience).

# WebSockets.FakeServer

Internal test infrastructure, not published as a NuGet package. Used by `WebSockets.Legacy.Tests` and `WebSockets.Modern.Tests` to reproduce and verify handshake behavior deterministically, without depending on a real external WebSocket server.

Full documentation, including how to control the handshake response, simulate dropped connections, and other common test scenarios, is at https://scottoffen.github.io/websockets/fakeserver.
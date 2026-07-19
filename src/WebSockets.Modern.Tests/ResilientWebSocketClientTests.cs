using System.Diagnostics;
using WebSockets.FakeServer;
using WebSockets.Resilience;

namespace WebSockets.Modern.Tests;

public class ResilientWebSocketClientTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task StartAsync_ConnectsAndEchoesMessages()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions { SupportsReconnection = true };
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        await using var resilient = await client.AsResilient(server.Uri).StartAsync();

        var received = new TaskCompletionSource<string>();
        resilient.MessageReceived += message => received.TrySetResult(message);

        await resilient.SendAsync("hello");

        var message = await AwaitOrTimeoutAsync(received.Task);
        message.ShouldBe("hello");
    }

    [Fact]
    public async Task DropCurrentConnection_TriggersReconnect()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions { SupportsReconnection = true };
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        await using var resilient = await client.AsResilient(server.Uri).StartAsync();

        server.ConnectionCount.ShouldBe(1);

        var reconnected = new TaskCompletionSource<bool>();
        resilient.Reconnected += () => reconnected.TrySetResult(true);

        await server.DropCurrentConnectionAsync();
        await AwaitOrTimeoutAsync(reconnected.Task);

        server.ConnectionCount.ShouldBe(2);
    }

    [Fact]
    public async Task Reconnected_DoesNotFire_OnInitialConnection_ButFiresAfterADrop()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions { SupportsReconnection = true };
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        var resilient = client.AsResilient(server.Uri);

        var reconnectedCount = 0;
        resilient.Reconnected += () => Interlocked.Increment(ref reconnectedCount);

        await using var started = await resilient.StartAsync();

        // Give the client a moment to prove nothing fires spuriously after
        // the initial connection.
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        reconnectedCount.ShouldBe(0);

        var reconnected = new TaskCompletionSource<bool>();
        resilient.Reconnected += () => reconnected.TrySetResult(true);

        await server.DropCurrentConnectionAsync();
        await AwaitOrTimeoutAsync(reconnected.Task);

        reconnectedCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_WhileDisconnected_IsDeliveredOnceReconnected()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions { SupportsReconnection = true };
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        await using var resilient = await client.AsResilient(server.Uri).StartAsync();

        var disconnected = new TaskCompletionSource<bool>();
        resilient.ReconnectFailed += _ => disconnected.TrySetResult(true);

        var received = new TaskCompletionSource<string>();
        resilient.MessageReceived += message => received.TrySetResult(message);

        await server.DropCurrentConnectionAsync();

        // Wait until the client has actually noticed the old connection is
        // gone (not just until the server-side socket was closed), so this
        // proves the queue's actual guarantee: a message sent while
        // genuinely disconnected is delivered once reconnected. Right at the
        // instant of the drop, TCP doesn't guarantee a concurrent send fails
        // just because the peer already reset the connection, so we don't
        // assert across that narrower, unclosable race window.
        await AwaitOrTimeoutAsync(disconnected.Task);

        await resilient.SendAsync("queued-during-gap");

        var message = await AwaitOrTimeoutAsync(received.Task);
        message.ShouldBe("queued-during-gap");
    }

    [Fact]
    public async Task ReceiveLoop_ReassemblesFragmentedMessages()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions
        {
            SupportsReconnection = true,
            FragmentEchoIntoChunksOfSize = 4,
        };
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        await using var resilient = await client.AsResilient(server.Uri).StartAsync();

        var received = new TaskCompletionSource<string>();
        resilient.MessageReceived += message => received.TrySetResult(message);

        const string original = "a longer message that the server will echo back fragmented";
        await resilient.SendAsync(original);

        var message = await AwaitOrTimeoutAsync(received.Task);
        message.ShouldBe(original);
    }

    [Fact]
    public async Task ReconnectFailed_Fires_WhenAReconnectAttemptFails_ThenRecoversOnALaterAttempt()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions
        {
            SupportsReconnection = true,
            FailFirstConnections = 1, // the first reconnect attempt (not the initial connection) fails
        };
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        await using var resilient = await client.AsResilient(server.Uri).StartAsync();

        server.ConnectionCount.ShouldBe(1);

        Exception? failure = null;
        var reconnectFailed = new TaskCompletionSource<bool>();
        resilient.ReconnectFailed += ex =>
        {
            failure = ex;
            reconnectFailed.TrySetResult(true);
        };

        var reconnected = new TaskCompletionSource<bool>();
        resilient.Reconnected += () => reconnected.TrySetResult(true);

        await server.DropCurrentConnectionAsync();

        // The first reconnect attempt should fail (connection #2, aborted
        // by FailFirstConnections), then the client should retry and the
        // second reconnect attempt (connection #3) should succeed.
        await AwaitOrTimeoutAsync(reconnectFailed.Task);
        failure.ShouldNotBeNull();

        await AwaitOrTimeoutAsync(reconnected.Task);
        server.ConnectionCount.ShouldBe(3);
    }

    [Fact]
    public async Task CloseAsync_FlushesQueuedMessage_BeforeTearingDown()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions { SupportsReconnection = true };
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        var resilient = await client.AsResilient(server.Uri).StartAsync();

        await resilient.SendAsync("flush-me");
        await resilient.CloseAsync(TimeSpan.FromSeconds(5));

        // Verify server-side receipt directly rather than relying on the
        // client's own MessageReceived/echo: CloseAsync only guarantees
        // queued sends go out before tearing down, it doesn't wait for a
        // response, so the client's receive loop can legitimately be gone
        // (as part of that same teardown) before an echo would arrive.
        server.LastMessageReceived.ShouldBe("flush-me");
    }

    [Fact]
    public async Task CloseAsync_ReturnsAfterTimeout_WhenMessageCannotBeFlushedInTime()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions { SupportsReconnection = true };
        var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        var resilient = await client.AsResilient(server.Uri).StartAsync();

        await server.DropCurrentConnectionAsync();

        // Stop the server entirely, not just drop the connection: any
        // reconnect attempt from here on genuinely fails (connection
        // refused), so the queued message below can never actually be sent.
        await server.DisposeAsync();

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await resilient.SendAsync("never-sent");

        var stopwatch = Stopwatch.StartNew();
        await resilient.CloseAsync(TimeSpan.FromSeconds(1));
        stopwatch.Stop();

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task CloseAsync_ThenDisposeAsync_DoesNotThrow()
    {
        var connectionOptions = new FakeWebSocketServerConnectionOptions { SupportsReconnection = true };
        await using var server = await FakeWebSocketServer.StartAsync("Upgrade", timeout: TimeSpan.FromSeconds(20), connectionOptions: connectionOptions);

        var client = new WebSocketClient();
        var resilient = await client.AsResilient(server.Uri).StartAsync();

        await resilient.CloseAsync(TimeSpan.FromSeconds(5));

        // Should be a safe no-op, not throw.
        await resilient.DisposeAsync();
    }

    private static async Task<T> AwaitOrTimeoutAsync<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.ShouldBeSameAs(task);
        return await task;
    }
}
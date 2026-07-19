using WebSockets.Resilience;

namespace WebSockets.Legacy.Tests;

public class ResilientWebSocketClientExtensionsTests
{
    [Fact]
    public void WithBackoffStrategy_SetsStrategy_AndReturnsSameInstance()
    {
        var resilient = new WebSocketClient().AsResilient(new Uri("ws://127.0.0.1/"));

        var result = resilient.WithBackoffStrategy(BackoffStrategy.Exponential);

        result.ShouldBeSameAs(resilient);
        resilient.Options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
    }

    [Fact]
    public void WithInitialReconnectDelay_SetsDelay_AndReturnsSameInstance()
    {
        var resilient = new WebSocketClient().AsResilient(new Uri("ws://127.0.0.1/"));
        var delay = TimeSpan.FromSeconds(5);

        var result = resilient.WithInitialReconnectDelay(delay);

        result.ShouldBeSameAs(resilient);
        resilient.Options.InitialReconnectDelay.ShouldBe(delay);
    }

    [Fact]
    public void WithMaxReconnectDelay_SetsDelay_AndReturnsSameInstance()
    {
        var resilient = new WebSocketClient().AsResilient(new Uri("ws://127.0.0.1/"));
        var delay = TimeSpan.FromMinutes(2);

        var result = resilient.WithMaxReconnectDelay(delay);

        result.ShouldBeSameAs(resilient);
        resilient.Options.MaxReconnectDelay.ShouldBe(delay);
    }
}
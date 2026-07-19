using WebSockets.Resilience;

namespace WebSockets.Legacy.Tests;

public class ResilienceExtensionsTests
{
    [Fact]
    public void AsResilient_WithOptions_UsesProvidedOptions()
    {
        var client = new WebSocketClient();
        var options = new ResilientWebSocketClientOptions { BackoffStrategy = BackoffStrategy.Linear };

        var resilient = client.AsResilient(new Uri("ws://127.0.0.1/"), options);

        resilient.Options.ShouldBeSameAs(options);
    }

    [Fact]
    public void AsResilient_WithConfigureAction_AppliesConfiguration()
    {
        var client = new WebSocketClient();

        var resilient = client.AsResilient(new Uri("ws://127.0.0.1/"), o => o.BackoffStrategy = BackoffStrategy.Exponential);

        resilient.Options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
    }
}
namespace WebSockets.Resilience;

/// <summary>
/// Fluent, chainable configuration extensions over <see cref="IResilientWebSocketClient"/>.
///
/// Deliberately covers only <see cref="ResilientWebSocketClientOptions.BackoffStrategy"/>,
/// <see cref="ResilientWebSocketClientOptions.InitialReconnectDelay"/>, and
/// <see cref="ResilientWebSocketClientOptions.MaxReconnectDelay"/>, all read
/// fresh on every reconnect attempt, so setting them here, even after
/// <see cref="IResilientWebSocketClient.StartAsync"/>, genuinely takes
/// effect. There's intentionally no <c>WithSendQueueCapacity</c>: that value
/// is only ever read once, in the constructor, to size the internal send
/// queue. Setting it here would compile and appear to work while silently
/// doing nothing, since the queue's already built by the time any of these
/// extensions could run. Set it via <see cref="ResilienceExtensions.AsResilient(IWebSocketClient,Uri,Action{ResilientWebSocketClientOptions})"/>
/// instead, which configures the options before construction.
/// </summary>
public static class ResilientWebSocketClientExtensions
{
    /// <summary>Sets <see cref="ResilientWebSocketClientOptions.BackoffStrategy"/>.</summary>
    public static IResilientWebSocketClient WithBackoffStrategy(this IResilientWebSocketClient client, BackoffStrategy strategy)
    {
        client.Options.BackoffStrategy = strategy;
        return client;
    }

    /// <summary>Sets <see cref="ResilientWebSocketClientOptions.InitialReconnectDelay"/>.</summary>
    public static IResilientWebSocketClient WithInitialReconnectDelay(this IResilientWebSocketClient client, TimeSpan delay)
    {
        client.Options.InitialReconnectDelay = delay;
        return client;
    }

    /// <summary>Sets <see cref="ResilientWebSocketClientOptions.MaxReconnectDelay"/>.</summary>
    public static IResilientWebSocketClient WithMaxReconnectDelay(this IResilientWebSocketClient client, TimeSpan delay)
    {
        client.Options.MaxReconnectDelay = delay;
        return client;
    }
}
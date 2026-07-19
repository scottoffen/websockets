namespace WebSockets.Resilience;

/// <summary>
/// The entry point into <see cref="ResilientWebSocketClient"/>: wraps any
/// <see cref="IWebSocketClient"/> (including a mock, for testing) as an
/// <see cref="IResilientWebSocketClient"/>.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>Wraps <paramref name="client"/> as a resilient client using the given (or default) options.</summary>
    public static IResilientWebSocketClient AsResilient(
        this IWebSocketClient client,
        Uri uri,
        ResilientWebSocketClientOptions? options = null)
        => new ResilientWebSocketClient(client, uri, options);

    /// <summary>Wraps <paramref name="client"/> as a resilient client, configuring a fresh <see cref="ResilientWebSocketClientOptions"/> inline.</summary>
    public static IResilientWebSocketClient AsResilient(
        this IWebSocketClient client,
        Uri uri,
        Action<ResilientWebSocketClientOptions> configureOptions)
    {
        var options = new ResilientWebSocketClientOptions();
        configureOptions(options);
        return new ResilientWebSocketClient(client, uri, options);
    }
}
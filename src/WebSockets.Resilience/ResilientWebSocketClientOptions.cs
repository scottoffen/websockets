namespace WebSockets.Resilience;

/// <summary>
/// Configuration for <see cref="ResilientWebSocketClient"/>.
/// </summary>
public sealed class ResilientWebSocketClientOptions
{
    /// <summary>
    /// How to space out reconnect attempts after a dropped connection.
    /// Defaults to <see cref="BackoffStrategy.None"/>, no delay at all,
    /// deliberately: see <see cref="BackoffStrategy.None"/> for why.
    /// </summary>
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.None;

    /// <summary>
    /// The base delay used by <see cref="BackoffStrategy.Linear"/> and
    /// <see cref="BackoffStrategy.Exponential"/>. Has no effect under
    /// <see cref="BackoffStrategy.None"/>. Defaults to 1 second.
    /// </summary>
    public TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The cap applied to the computed delay under <see cref="BackoffStrategy.Linear"/>
    /// and <see cref="BackoffStrategy.Exponential"/>. Has no effect under
    /// <see cref="BackoffStrategy.None"/>. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The maximum number of not-yet-sent messages buffered while
    /// disconnected. Defaults to 256.
    /// </summary>
    public int SendQueueCapacity { get; set; } = 256;
}
namespace WebSockets.Resilience;

/// <summary>
/// Pure backoff-delay calculation, extracted from <see cref="ResilientWebSocketClient"/>
/// so it can be unit tested directly rather than needing to exercise real
/// reconnect timing.
/// </summary>
public static class ReconnectBackoff
{
    /// <summary>
    /// Computes the delay before reconnect attempt <paramref name="attempt"/>
    /// (1-based), per <paramref name="strategy"/>, capped at <paramref name="maxDelay"/>.
    /// </summary>
    public static TimeSpan GetDelay(BackoffStrategy strategy, TimeSpan initialDelay, TimeSpan maxDelay, int attempt)
    {
        return strategy switch
        {
            BackoffStrategy.None => TimeSpan.Zero,
            BackoffStrategy.Linear => Min(maxDelay, Scale(initialDelay, attempt)),
            BackoffStrategy.Exponential => Min(maxDelay, Scale(initialDelay, Math.Pow(2, attempt - 1))),
            _ => TimeSpan.Zero,
        };
    }

    private static TimeSpan Scale(TimeSpan baseDelay, double factor) => TimeSpan.FromTicks((long)(baseDelay.Ticks * factor));

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
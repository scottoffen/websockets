namespace WebSockets.Resilience;

/// <summary>
/// How long to wait between reconnect attempts after a dropped connection.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// No delay between reconnect attempts, retries immediately. This is the
    /// default deliberately: it's a real behavior (and a real risk, hammering
    /// a server that's genuinely down) rather than a hidden fixed delay, so
    /// using this in production requires actively choosing it rather than
    /// inheriting an assumption made on your behalf.
    /// </summary>
    None,

    /// <summary>
    /// Delay grows linearly with each attempt: <c>InitialReconnectDelay * attempt</c>,
    /// capped at <c>MaxReconnectDelay</c>.
    /// </summary>
    Linear,

    /// <summary>
    /// Delay doubles with each attempt: <c>InitialReconnectDelay * 2^(attempt - 1)</c>,
    /// capped at <c>MaxReconnectDelay</c>.
    /// </summary>
    Exponential
}
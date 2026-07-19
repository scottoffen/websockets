using WebSockets.Resilience;

namespace WebSockets.Legacy.Tests;

public class ReconnectBackoffTests
{
    [Fact]
    public void GetDelay_ReturnsZero_ForNoneStrategy()
    {
        var delay = ReconnectBackoff.GetDelay(BackoffStrategy.None, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), attempt: 5);

        delay.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void GetDelay_GrowsLinearly_ForLinearStrategy(int attempt, int expectedSeconds)
    {
        var delay = ReconnectBackoff.GetDelay(BackoffStrategy.Linear, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), attempt);

        delay.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    public void GetDelay_DoublesEachAttempt_ForExponentialStrategy(int attempt, int expectedSeconds)
    {
        var delay = ReconnectBackoff.GetDelay(BackoffStrategy.Exponential, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), attempt);

        delay.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void GetDelay_IsCappedByMaxReconnectDelay_ForLinearStrategy()
    {
        var delay = ReconnectBackoff.GetDelay(BackoffStrategy.Linear, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), attempt: 50);

        delay.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void GetDelay_IsCappedByMaxReconnectDelay_ForExponentialStrategy()
    {
        var delay = ReconnectBackoff.GetDelay(BackoffStrategy.Exponential, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), attempt: 10);

        delay.ShouldBe(TimeSpan.FromSeconds(10));
    }
}
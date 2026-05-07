using MonetaCore.Services;

namespace MonetaCore.Tests;

public class OutboxDispatcherBackoffTests
{
    [Fact]
    public void ComputeRetryDelay_UsesExponentialGrowthAndCap_WhenJitterDisabled()
    {
        var options = new OutboxDispatcherOptions
        {
            RetryBaseDelaySeconds = 5,
            RetryMaxDelaySeconds = 40,
            RetryJitterSeconds = 0
        };

        TimeSpan first = OutboxDispatcherBackgroundService.ComputeRetryDelay(1, options);
        TimeSpan second = OutboxDispatcherBackgroundService.ComputeRetryDelay(2, options);
        TimeSpan third = OutboxDispatcherBackgroundService.ComputeRetryDelay(3, options);
        TimeSpan fourth = OutboxDispatcherBackgroundService.ComputeRetryDelay(4, options);
        TimeSpan fifth = OutboxDispatcherBackgroundService.ComputeRetryDelay(5, options);

        Assert.Equal(TimeSpan.FromSeconds(5), first);
        Assert.Equal(TimeSpan.FromSeconds(10), second);
        Assert.Equal(TimeSpan.FromSeconds(20), third);
        Assert.Equal(TimeSpan.FromSeconds(40), fourth);
        Assert.Equal(TimeSpan.FromSeconds(40), fifth);
    }
}

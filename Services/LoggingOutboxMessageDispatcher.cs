using MonetaCore.Models;

namespace MonetaCore.Services;

public class LoggingOutboxMessageDispatcher : IOutboxMessageDispatcher
{
    private readonly ILogger<LoggingOutboxMessageDispatcher> _logger;

    public LoggingOutboxMessageDispatcher(ILogger<LoggingOutboxMessageDispatcher> logger)
    {
        _logger = logger;
    }

    public Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Outbox event dispatched. EventId: {EventId}, EventType: {EventType}, Producer: {Producer}",
            message.EventId,
            message.EventType,
            message.Producer);

        return Task.CompletedTask;
    }
}

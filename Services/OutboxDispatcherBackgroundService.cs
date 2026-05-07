using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MonetaCore.Data;
using MonetaCore.Models;

namespace MonetaCore.Services;

public class OutboxDispatcherOptions
{
    public int PollIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public int RetryBaseDelaySeconds { get; set; } = 5;
    public int RetryMaxDelaySeconds { get; set; } = 300;
    public int RetryJitterSeconds { get; set; } = 3;
}

public class OutboxDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OutboxDispatcherOptions> _options;
    private readonly ILogger<OutboxDispatcherBackgroundService> _logger;

    public OutboxDispatcherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OutboxDispatcherOptions> options,
        ILogger<OutboxDispatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox dispatcher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            int processedCount = 0;

            try
            {
                processedCount = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox batch dispatch failed.");
            }

            if (processedCount == 0)
            {
                int waitSeconds = Math.Max(1, _options.CurrentValue.PollIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(waitSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("Outbox dispatcher stopped.");
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxMessageDispatcher>();

        OutboxDispatcherOptions options = _options.CurrentValue;
        int batchSize = Math.Max(1, options.BatchSize);
        int maxAttempts = Math.Max(1, options.MaxAttempts);
        DateTime utcNow = DateTime.UtcNow;

        List<OutboxMessage> pending = await dbContext.OutboxMessages
            .Where(x => x.Status == DomainValues.OutboxStatus.Pending
                && x.AttemptCount < maxAttempts
                && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= utcNow))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        foreach (OutboxMessage message in pending)
        {
            message.AttemptCount += 1;
            message.LastAttemptedAtUtc = DateTime.UtcNow;

            try
            {
                await dispatcher.DispatchAsync(message, cancellationToken);
                message.Status = DomainValues.OutboxStatus.Dispatched;
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = string.Empty;
                message.NextAttemptAtUtc = null;

                dbContext.IntegrationEvents.Add(BuildIntegrationEvent(
                    message,
                    DomainValues.SyncStatus.Success,
                    "Outbox event dispatched by background connector."));
            }
            catch (Exception exception)
            {
                string error = exception.Message;
                message.LastError = error.Length > 600 ? error[..600] : error;

                if (message.AttemptCount >= maxAttempts)
                {
                    message.Status = DomainValues.OutboxStatus.Failed;
                    message.NextAttemptAtUtc = null;

                    dbContext.IntegrationEvents.Add(BuildIntegrationEvent(
                        message,
                        DomainValues.SyncStatus.Failed,
                        message.LastError));
                }
                else
                {
                    TimeSpan delay = ComputeRetryDelay(message.AttemptCount, options);
                    message.NextAttemptAtUtc = DateTime.UtcNow.Add(delay);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }

    public static TimeSpan ComputeRetryDelay(int attemptCount, OutboxDispatcherOptions options)
    {
        int normalizedAttempt = Math.Max(1, attemptCount);
        double baseSeconds = Math.Max(1, options.RetryBaseDelaySeconds);
        double maxSeconds = Math.Max(baseSeconds, options.RetryMaxDelaySeconds);

        double exponential = baseSeconds * Math.Pow(2, normalizedAttempt - 1);
        double capped = Math.Min(maxSeconds, exponential);

        double jitterRange = Math.Max(0, options.RetryJitterSeconds);
        double jitter = jitterRange == 0
            ? 0
            : (Random.Shared.NextDouble() * 2 - 1) * jitterRange;

        double totalSeconds = Math.Max(1, capped + jitter);
        return TimeSpan.FromSeconds(totalSeconds);
    }

    private static AccountIntegrationEvent BuildIntegrationEvent(
        OutboxMessage message,
        string status,
        string details)
    {
        return new AccountIntegrationEvent
        {
            Provider = "OutboxConnector",
            Direction = "Export",
            Payload = message.PayloadJson,
            Status = status,
            Message = Truncate(details, 500),
            CorrelationId = message.CorrelationId,
            SyncedAtUtc = DateTime.UtcNow
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Models;

namespace MonetaCore.Services;

public class EventOutboxService : IEventOutboxService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EventOutboxService> _logger;
    private readonly SemaphoreSlim _fileGate = new(1, 1);

    public EventOutboxService(
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        ILogger<EventOutboxService> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
    }

    public async Task<Guid> QueueAsync(DomainEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var message = OutboxMessage.FromEnvelope(envelope);

        try
        {
            _dbContext.OutboxMessages.Add(message);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await PersistFallbackAsync(message, exception, cancellationToken);
        }

        return message.EventId;
    }

    public Task<Guid> QueueAsync<TPayload>(
        string eventType,
        string producer,
        TPayload payload,
        string correlationId = "",
        string causationId = "",
        string idempotencyKey = "",
        string tenantId = "",
        CancellationToken cancellationToken = default)
    {
        var envelope = DomainEventEnvelope.Create(
            eventType,
            producer,
            payload,
            correlationId,
            causationId,
            idempotencyKey,
            tenantId);

        return QueueAsync(envelope, cancellationToken);
    }

    private async Task PersistFallbackAsync(
        OutboxMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string appDataPath = Path.Combine(_environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);
        string fallbackPath = Path.Combine(appDataPath, "outbox-fallback.ndjson");

        var fallbackRecord = new
        {
            message.EventId,
            message.EventType,
            message.PayloadVersion,
            message.Producer,
            message.CorrelationId,
            message.CausationId,
            message.IdempotencyKey,
            message.TenantId,
            message.OccurredAtUtc,
            message.PayloadJson,
            PersistedAtUtc = DateTime.UtcNow,
            Error = exception.Message
        };

        string serialized = JsonSerializer.Serialize(fallbackRecord, SerializerOptions);

        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                fallbackPath,
                serialized + Environment.NewLine,
                cancellationToken);
        }
        finally
        {
            _fileGate.Release();
        }

        _logger.LogWarning(exception, "Outbox fallback persisted for event {EventId} ({EventType}).", message.EventId, message.EventType);
    }
}

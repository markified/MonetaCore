using MonetaCore.Models;

namespace MonetaCore.Services;

public interface IEventOutboxService
{
    Task<Guid> QueueAsync(DomainEventEnvelope envelope, CancellationToken cancellationToken = default);

    Task<Guid> QueueAsync<TPayload>(
        string eventType,
        string producer,
        TPayload payload,
        string correlationId = "",
        string causationId = "",
        string idempotencyKey = "",
        string tenantId = "",
        CancellationToken cancellationToken = default);
}

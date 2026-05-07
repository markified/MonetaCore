using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MonetaCore.Models;

public class DomainEventEnvelope
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Guid EventId { get; set; } = Guid.NewGuid();

    [Required, StringLength(120)]
    public string EventType { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string PayloadVersion { get; set; } = "1.0";

    [Required, StringLength(120)]
    public string Producer { get; set; } = string.Empty;

    [StringLength(120)]
    public string CorrelationId { get; set; } = string.Empty;

    [StringLength(120)]
    public string CausationId { get; set; } = string.Empty;

    [StringLength(120)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [StringLength(80)]
    public string TenantId { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public string PayloadJson { get; set; } = "{}";

    public static DomainEventEnvelope Create<TPayload>(
        string eventType,
        string producer,
        TPayload payload,
        string correlationId = "",
        string causationId = "",
        string idempotencyKey = "",
        string tenantId = "")
    {
        return new DomainEventEnvelope
        {
            EventType = eventType,
            Producer = producer,
            CorrelationId = correlationId,
            CausationId = causationId,
            IdempotencyKey = idempotencyKey,
            TenantId = tenantId,
            PayloadJson = JsonSerializer.Serialize(payload, SerializerOptions)
        };
    }
}

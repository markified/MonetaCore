using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class OutboxMessage
{
    public long Id { get; set; }

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

    [Required, StringLength(30)]
    public string Status { get; set; } = DomainValues.OutboxStatus.Pending;

    public int AttemptCount { get; set; }

    [StringLength(600)]
    public string LastError { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }

    public static OutboxMessage FromEnvelope(DomainEventEnvelope envelope)
    {
        return new OutboxMessage
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            PayloadVersion = envelope.PayloadVersion,
            Producer = envelope.Producer,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            IdempotencyKey = envelope.IdempotencyKey,
            TenantId = envelope.TenantId,
            OccurredAtUtc = envelope.OccurredAtUtc,
            PayloadJson = envelope.PayloadJson,
            Status = DomainValues.OutboxStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow
        };
    }
}

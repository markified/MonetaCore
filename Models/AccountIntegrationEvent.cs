using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class AccountIntegrationEvent
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Provider { get; set; } = "ExternalAccounting";

    [Required, StringLength(30)]
    public string Direction { get; set; } = "Export";

    [Required]
    public string Payload { get; set; } = "{}";

    [Required, StringLength(30)]
    public string Status { get; set; } = DomainValues.SyncStatus.Pending;

    [StringLength(500)]
    public string Message { get; set; } = string.Empty;

    [StringLength(120)]
    public string CorrelationId { get; set; } = string.Empty;

    public int? TriggeredByUserId { get; set; }
    public AppUser? TriggeredByUser { get; set; }

    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}

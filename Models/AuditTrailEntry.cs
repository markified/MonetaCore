using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class AuditTrailEntry
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public AppUser? User { get; set; }

    [StringLength(180)]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(80)]
    public string EntityId { get; set; } = string.Empty;

    [StringLength(500)]
    public string Metadata { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

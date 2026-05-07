using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class PortalDispute
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string DisputeReference { get; set; } = string.Empty;

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int? SubmittedByUserId { get; set; }
    public AppUser? SubmittedByUser { get; set; }

    [Required, StringLength(120)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Status { get; set; } = DomainValues.DisputeStatus.Submitted;

    [StringLength(500)]
    public string ResolutionNotes { get; set; } = string.Empty;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}

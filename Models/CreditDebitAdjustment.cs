using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class CreditDebitAdjustment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public int? ApprovedByUserId { get; set; }
    public AppUser? ApprovedByUser { get; set; }

    [Required, StringLength(30)]
    public string Type { get; set; } = DomainValues.AdjustmentType.CreditNote;

    public decimal Amount { get; set; }

    [StringLength(320)]
    public string Reason { get; set; } = string.Empty;

    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}

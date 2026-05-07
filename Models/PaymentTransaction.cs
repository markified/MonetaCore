using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class PaymentTransaction
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int? ProcessedByUserId { get; set; }
    public AppUser? ProcessedByUser { get; set; }

    public decimal Amount { get; set; }

    [Required, StringLength(60)]
    public string Method { get; set; } = DomainValues.PaymentMethod.Cash;

    [StringLength(120)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [StringLength(120)]
    public string GatewayTransactionId { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Status { get; set; } = DomainValues.PaymentStatus.Pending;

    [StringLength(300)]
    public string Notes { get; set; } = string.Empty;

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

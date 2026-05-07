using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class Invoice
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public int ClientAccountId { get; set; }
    public ClientAccount? ClientAccount { get; set; }

    public int CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public DateTime IssueDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime DueDateUtc { get; set; } = DateTime.UtcNow.AddDays(15);

    [Required, StringLength(30)]
    public string Status { get; set; } = DomainValues.InvoiceStatus.Issued;

    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal AdjustmentTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceLineItem> Items { get; set; } = new List<InvoiceLineItem>();
    public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
    public ICollection<CreditDebitAdjustment> Adjustments { get; set; } = new List<CreditDebitAdjustment>();
    public ICollection<PortalDispute> Disputes { get; set; } = new List<PortalDispute>();
}

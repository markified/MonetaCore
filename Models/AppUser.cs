using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Role { get; set; } = ApplicationRoles.BillingStaff;

    public bool IsActive { get; set; } = true;

    public int? ClientAccountId { get; set; }
    public ClientAccount? ClientAccount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Invoice> AuthoredInvoices { get; set; } = new List<Invoice>();
    public ICollection<PaymentTransaction> ProcessedPayments { get; set; } = new List<PaymentTransaction>();
    public ICollection<CreditDebitAdjustment> CreatedAdjustments { get; set; } = new List<CreditDebitAdjustment>();
    public ICollection<CreditDebitAdjustment> ApprovedAdjustments { get; set; } = new List<CreditDebitAdjustment>();
    public ICollection<PortalDispute> SubmittedPortalDisputes { get; set; } = new List<PortalDispute>();
    public ICollection<AuditTrailEntry> AuditTrailEntries { get; set; } = new List<AuditTrailEntry>();
}

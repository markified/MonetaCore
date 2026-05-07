using System.ComponentModel.DataAnnotations;
using MonetaCore.Models;

namespace MonetaCore.ViewModels;

public class PaymentCreateViewModel
{
    [Required]
    public int InvoiceId { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [Required, StringLength(60)]
    [RegularExpression(@"^(Cash|PayMongo)$", ErrorMessage = "Method must be Cash or PayMongo.")]
    public string Method { get; set; } = string.Empty;

    [StringLength(120)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Notes { get; set; }

    [StringLength(30)]
    [RegularExpression(@"^(Checkout|Card)$", ErrorMessage = "PayMongo flow must be Checkout or Card.")]
    public string PayMongoFlow { get; set; } = DomainValues.PayMongoFlow.Checkout;

    [StringLength(80)]
    [Display(Name = "Cardholder Name")]
    public string? CardholderName { get; set; }

    [StringLength(19)]
    [Display(Name = "Card Number")]
    public string? CardNumber { get; set; }

    [Range(1, 12)]
    [Display(Name = "Expiry Month")]
    public int? CardExpMonth { get; set; }

    [Range(2020, 2100)]
    [Display(Name = "Expiry Year")]
    public int? CardExpYear { get; set; }

    [StringLength(4)]
    [Display(Name = "CVC")]
    public string? CardCvc { get; set; }
}

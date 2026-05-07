using System.ComponentModel.DataAnnotations;
using MonetaCore.Models;

namespace MonetaCore.ApiModels;

/// <summary>
/// Request payload for creating a payment transaction.
/// </summary>
public class ApiPaymentCreateRequest
{
    [Required]
    public int InvoiceId { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    /// <summary>
    /// Payment method. Allowed values: Cash, PayMongo.
    /// </summary>
    /// <example>Cash</example>
    [Required, StringLength(60)]
    [RegularExpression(@"^(Cash|PayMongo)$", ErrorMessage = "Method must be Cash or PayMongo.")]
    public string Method { get; set; } = DomainValues.PaymentMethod.Cash;

    [StringLength(120)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Notes { get; set; }

    /// <summary>
    /// PayMongo flow when Method is PayMongo. Allowed values: Checkout, Card.
    /// </summary>
    /// <example>Checkout</example>
    [StringLength(30)]
    [RegularExpression(@"^(Checkout|Card)$", ErrorMessage = "PayMongoFlow must be Checkout or Card.")]
    public string PayMongoFlow { get; set; } = DomainValues.PayMongoFlow.Checkout;

    [StringLength(80)]
    public string? CardholderName { get; set; }

    [StringLength(19)]
    public string? CardNumber { get; set; }

    [Range(1, 12)]
    public int? CardExpMonth { get; set; }

    [Range(2020, 2100)]
    public int? CardExpYear { get; set; }

    [StringLength(4)]
    public string? CardCvc { get; set; }
}

public class ApiPaymentDto
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string GatewayTransactionId { get; set; } = string.Empty;
    public DateTime PaidAtUtc { get; set; }
    public string NextActionUrl { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace MonetaCore.ApiModels;

public class ApiInvoiceItemRequest
{
    [Required, StringLength(220)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999999)]
    public decimal Quantity { get; set; }

    [Range(0.01, 999999999)]
    public decimal UnitPrice { get; set; }
}

public class ApiInvoiceCreateRequest
{
    [Required]
    public int ClientAccountId { get; set; }

    [Required]
    public DateTime DueDateUtc { get; set; }

    [Range(0, 1)]
    public decimal TaxRate { get; set; } = 0.12m;

    [StringLength(500)]
    public string? Notes { get; set; }

    [Required]
    public List<ApiInvoiceItemRequest> Items { get; set; } = new();
}

public class ApiInvoiceUpdateRequest
{
    public DateTime? DueDateUtc { get; set; }

    [Range(0, 1)]
    public decimal? TaxRate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    public List<ApiInvoiceItemRequest>? Items { get; set; }
}

public class ApiInvoiceSummaryDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int ClientAccountId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public DateTime IssueDateUtc { get; set; }
    public DateTime DueDateUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal BalanceDue { get; set; }
}

public class ApiInvoiceLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class ApiInvoiceDetailDto : ApiInvoiceSummaryDto
{
    public decimal TaxRate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal AdjustmentTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<ApiInvoiceLineItemDto> Items { get; set; } = new();
}

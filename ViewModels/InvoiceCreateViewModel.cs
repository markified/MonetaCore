using System.ComponentModel.DataAnnotations;

namespace MonetaCore.ViewModels;

public class InvoiceCreateViewModel
{
    [Required]
    public int ClientAccountId { get; set; }

    [Required]
    public DateTime DueDateUtc { get; set; } = DateTime.UtcNow.Date.AddDays(15);

    [Range(0, 1)]
    public decimal TaxRate { get; set; } = 0.12m;

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<InvoiceItemInputModel> Items { get; set; } =
    [
        new InvoiceItemInputModel()
    ];
}

public class InvoiceItemInputModel
{
    [Required, StringLength(220)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999999)]
    public decimal Quantity { get; set; } = 1;

    [Range(0.01, 999999999)]
    public decimal UnitPrice { get; set; } = 1;
}

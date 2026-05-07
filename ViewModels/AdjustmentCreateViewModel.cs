using System.ComponentModel.DataAnnotations;

namespace MonetaCore.ViewModels;

public class AdjustmentCreateViewModel
{
    [Required]
    public int InvoiceId { get; set; }

    [Required, StringLength(30)]
    public string Type { get; set; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(320)]
    public string? Reason { get; set; }
}

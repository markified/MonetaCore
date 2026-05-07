using MonetaCore.Models;

namespace MonetaCore.ViewModels;

public class InvoiceIndexViewModel
{
    public string Search { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;

    public int DraftCount { get; set; }
    public int IssuedCount { get; set; }
    public int PartiallyPaidCount { get; set; }
    public int PaidCount { get; set; }
    public int OverdueCount { get; set; }

    public IReadOnlyList<Invoice> Invoices { get; set; } = [];
}

namespace MonetaCore.ViewModels;

public class ProfitLossReportViewModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedAtLocal { get; set; }

    public decimal RevenueTotal { get; set; }
    public decimal ExpenseTotal { get; set; }

    public decimal NetProfit => RevenueTotal - ExpenseTotal;

    public decimal ProfitMarginPercent => RevenueTotal <= 0
        ? 0
        : Math.Round((NetProfit / RevenueTotal) * 100m, 2, MidpointRounding.AwayFromZero);

    public string PeriodLabel => $"{StartDate:MMM dd, yyyy} - {EndDate:MMM dd, yyyy}";

    public IReadOnlyList<ProfitLossRevenueEntryViewModel> RevenueEntries { get; set; } = [];
    public IReadOnlyList<ProfitLossExpenseEntryViewModel> ExpenseEntries { get; set; } = [];
}

public class ProfitLossRevenueEntryViewModel
{
    public DateTime PaidAtUtc { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ProfitLossExpenseEntryViewModel
{
    public DateTime AppliedAtUtc { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
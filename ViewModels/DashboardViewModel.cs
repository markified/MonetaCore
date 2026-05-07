namespace MonetaCore.ViewModels;

public class DashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal OutstandingReceivables { get; set; }
    public decimal OverdueReceivables { get; set; }
    public int OpenInvoices { get; set; }
    public int CompletedPayments { get; set; }

    public IReadOnlyList<RecentInvoiceVm> RecentInvoices { get; set; } = [];
    public IReadOnlyList<TrendPointVm> RevenueTrend { get; set; } = [];
    public IReadOnlyList<TopClientVm> TopClients { get; set; } = [];

    public sealed class RecentInvoiceVm
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal BalanceDue { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DueDateUtc { get; set; }
    }

    public sealed class TrendPointVm
    {
        public string MonthLabel { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }

    public sealed class TopClientVm
    {
        public string ClientName { get; set; } = string.Empty;
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
    }
}

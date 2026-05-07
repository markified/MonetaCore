using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.ViewModels;

namespace MonetaCore.Controllers;

[Authorize]
[RequireModule(SystemModule.RevenueMonitoring, SystemModule.ViewReportsDashboards)]
public class DashboardController : Controller
{
    private readonly AppDbContext _dbContext;

    public DashboardController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        DateTime utcNow = DateTime.UtcNow;
        DateTime monthStart = new DateTime(utcNow.Year, utcNow.Month, 1);

        IQueryable<Invoice> invoiceQuery = _dbContext.Invoices
            .Include(x => x.ClientAccount)
            .AsNoTracking();

        IQueryable<PaymentTransaction> paymentQuery = _dbContext.Payments
            .AsNoTracking()
            .Where(x => x.Status == DomainValues.PaymentStatus.Completed);

        int? clientAccountId = GetClientAccountId();
        if (User.IsInRole(ApplicationRoles.Client) && clientAccountId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(x => x.ClientAccountId == clientAccountId.Value);
            paymentQuery = paymentQuery.Where(x => x.Invoice!.ClientAccountId == clientAccountId.Value);
        }

        decimal totalRevenue = await paymentQuery.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        decimal revenueThisMonth = await paymentQuery
            .Where(x => x.PaidAtUtc >= monthStart)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        decimal outstanding = await invoiceQuery
            .Where(x => x.Status != DomainValues.InvoiceStatus.Paid && x.Status != DomainValues.InvoiceStatus.Cancelled)
            .SumAsync(x => (decimal?)x.BalanceDue, cancellationToken) ?? 0m;

        decimal overdue = await invoiceQuery
            .Where(x => x.DueDateUtc < utcNow && x.BalanceDue > 0)
            .SumAsync(x => (decimal?)x.BalanceDue, cancellationToken) ?? 0m;

        int openInvoices = await invoiceQuery.CountAsync(x => x.BalanceDue > 0, cancellationToken);
        int completedPayments = await paymentQuery.CountAsync(cancellationToken);

        var recentInvoices = await invoiceQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(8)
            .Select(x => new DashboardViewModel.RecentInvoiceVm
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                ClientName = x.ClientAccount != null ? x.ClientAccount.CompanyName : "Unknown",
                TotalAmount = x.TotalAmount,
                BalanceDue = x.BalanceDue,
                Status = x.Status,
                DueDateUtc = x.DueDateUtc
            })
            .ToListAsync(cancellationToken);

        DateTime trendStart = new DateTime(utcNow.Year, utcNow.Month, 1).AddMonths(-5);
        var monthlyRevenue = await paymentQuery
            .Where(x => x.PaidAtUtc >= trendStart)
            .GroupBy(x => new { x.PaidAtUtc.Year, x.PaidAtUtc.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(v => v.Amount) })
            .ToListAsync(cancellationToken);

        var trend = Enumerable.Range(0, 6)
            .Select(i => trendStart.AddMonths(i))
            .Select(date =>
            {
                decimal value = monthlyRevenue
                    .Where(x => x.Year == date.Year && x.Month == date.Month)
                    .Select(x => x.Total)
                    .FirstOrDefault();

                return new DashboardViewModel.TrendPointVm
                {
                    MonthLabel = date.ToString("MMM yyyy"),
                    Revenue = value
                };
            })
            .ToList();

        var topClients = await invoiceQuery
            .GroupBy(x => x.ClientAccount != null ? x.ClientAccount.CompanyName : "Unknown")
            .Select(g => new DashboardViewModel.TopClientVm
            {
                ClientName = g.Key,
                TotalInvoiced = g.Sum(v => v.TotalAmount),
                TotalPaid = g.Sum(v => v.AmountPaid)
            })
            .OrderByDescending(x => x.TotalInvoiced)
            .Take(5)
            .ToListAsync(cancellationToken);

        var vm = new DashboardViewModel
        {
            TotalRevenue = totalRevenue,
            RevenueThisMonth = revenueThisMonth,
            OutstandingReceivables = outstanding,
            OverdueReceivables = overdue,
            OpenInvoices = openInvoices,
            CompletedPayments = completedPayments,
            RecentInvoices = recentInvoices,
            RevenueTrend = trend,
            TopClients = topClients
        };

        return View(vm);
    }

    private int? GetClientAccountId()
    {
        string? claim = User.FindFirst("ClientAccountId")?.Value;
        return int.TryParse(claim, out int id) ? id : null;
    }
}

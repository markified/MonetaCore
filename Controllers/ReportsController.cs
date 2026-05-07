using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.Services;
using MonetaCore.ViewModels;
using QuestPDF.Fluent;

namespace MonetaCore.Controllers;

[Authorize(Policy = AuthorizationPolicies.FinanceManagerOnly)]
[RequireModule(SystemModule.RevenueMonitoring, SystemModule.ViewReportsDashboards)]
public class ReportsController : Controller
{
    private readonly AppDbContext _dbContext;

    public ReportsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        ProfitLossReportViewModel model = await BuildProfitLossReportAsync(startDate, endDate, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportProfitLossPdf(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        ProfitLossReportViewModel model = await BuildProfitLossReportAsync(startDate, endDate, cancellationToken);
        byte[] pdf = new ProfitLossPdfDocument(model).GeneratePdf();

        string fileName = $"MonetaCore-PnL-{model.StartDate:yyyyMMdd}-{model.EndDate:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    private async Task<ProfitLossReportViewModel> BuildProfitLossReportAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        DateTime today = DateTime.UtcNow.Date;
        DateTime resolvedStartDate = startDate?.Date ?? new DateTime(today.Year, today.Month, 1);
        DateTime resolvedEndDate = endDate?.Date ?? today;

        if (resolvedEndDate < resolvedStartDate)
        {
            resolvedEndDate = resolvedStartDate;
        }

        DateTime rangeStartUtc = DateTime.SpecifyKind(resolvedStartDate, DateTimeKind.Utc);
        DateTime rangeEndExclusiveUtc = DateTime.SpecifyKind(resolvedEndDate.AddDays(1), DateTimeKind.Utc);

        List<ProfitLossRevenueEntryViewModel> revenueEntries = await _dbContext.Payments
            .AsNoTracking()
            .Where(x => x.Status == DomainValues.PaymentStatus.Completed)
            .Where(x => x.PaidAtUtc >= rangeStartUtc && x.PaidAtUtc < rangeEndExclusiveUtc)
            .OrderByDescending(x => x.PaidAtUtc)
            .Select(x => new ProfitLossRevenueEntryViewModel
            {
                PaidAtUtc = x.PaidAtUtc,
                InvoiceNumber = x.Invoice != null ? x.Invoice.InvoiceNumber : "Unlinked",
                ClientName = x.Invoice != null && x.Invoice.ClientAccount != null ? x.Invoice.ClientAccount.CompanyName : "Unknown",
                Method = x.Method,
                ReferenceNumber = x.ReferenceNumber,
                Amount = x.Amount
            })
            .ToListAsync(cancellationToken);

        List<ProfitLossExpenseEntryViewModel> expenseEntries = await _dbContext.Adjustments
            .AsNoTracking()
            .Where(x => x.Type == DomainValues.AdjustmentType.CreditNote)
            .Where(x => x.AppliedAtUtc >= rangeStartUtc && x.AppliedAtUtc < rangeEndExclusiveUtc)
            .OrderByDescending(x => x.AppliedAtUtc)
            .Select(x => new ProfitLossExpenseEntryViewModel
            {
                AppliedAtUtc = x.AppliedAtUtc,
                InvoiceNumber = x.Invoice != null ? x.Invoice.InvoiceNumber : "Unlinked",
                ClientName = x.Invoice != null && x.Invoice.ClientAccount != null ? x.Invoice.ClientAccount.CompanyName : "Unknown",
                Reason = x.Reason,
                Amount = x.Amount
            })
            .ToListAsync(cancellationToken);

        return new ProfitLossReportViewModel
        {
            StartDate = resolvedStartDate,
            EndDate = resolvedEndDate,
            GeneratedAtLocal = DateTime.Now,
            RevenueTotal = revenueEntries.Sum(x => x.Amount),
            ExpenseTotal = expenseEntries.Sum(x => x.Amount),
            RevenueEntries = revenueEntries,
            ExpenseEntries = expenseEntries
        };
    }
}
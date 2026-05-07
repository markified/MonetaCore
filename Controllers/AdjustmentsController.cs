using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.Services;
using MonetaCore.ViewModels;

namespace MonetaCore.Controllers;

[Authorize(Policy = AuthorizationPolicies.FinanceOperations)]
[RequireModule(SystemModule.CreditDebitManagement)]
public class AdjustmentsController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public AdjustmentsController(AppDbContext dbContext, ICurrentUserService currentUser, IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var adjustments = await _dbContext.Adjustments
            .AsNoTracking()
            .Include(x => x.Invoice)
            .ThenInclude(x => x!.ClientAccount)
            .Include(x => x.CreatedByUser)
            .OrderByDescending(x => x.AppliedAtUtc)
            .ToListAsync(cancellationToken);

        return View(adjustments);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateInvoiceDropDownAsync(cancellationToken);
        ViewBag.Types = new SelectList(new[]
        {
            DomainValues.AdjustmentType.CreditNote,
            DomainValues.AdjustmentType.DebitMemo
        });

        return View(new AdjustmentCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdjustmentCreateViewModel model, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == model.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            ModelState.AddModelError(nameof(model.InvoiceId), "Invoice not found.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateInvoiceDropDownAsync(cancellationToken);
            ViewBag.Types = new SelectList(new[]
            {
                DomainValues.AdjustmentType.CreditNote,
                DomainValues.AdjustmentType.DebitMemo
            });
            return View(model);
        }

        decimal signedAmount = model.Type == DomainValues.AdjustmentType.CreditNote
            ? -Math.Abs(model.Amount)
            : Math.Abs(model.Amount);

        invoice!.AdjustmentTotal += signedAmount;
        InvoiceCalculator.RecalculateTotals(invoice);

        var adjustment = new CreditDebitAdjustment
        {
            InvoiceId = invoice.Id,
            CreatedByUserId = _currentUser.UserId ?? 0,
            ApprovedByUserId = _currentUser.UserId,
            Type = model.Type,
            Amount = model.Amount,
            Reason = string.IsNullOrWhiteSpace(model.Reason) ? string.Empty : model.Reason.Trim(),
            AppliedAtUtc = DateTime.UtcNow
        };

        _dbContext.Adjustments.Add(adjustment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "ADJUSTMENT",
            "Invoice",
            invoice.Id.ToString(),
            $"{model.Type} {model.Amount:N2}",
            cancellationToken);

        TempData["Success"] = "Adjustment applied successfully.";
        return RedirectToAction("Details", "Invoices", new { id = invoice.Id });
    }

    private async Task PopulateInvoiceDropDownAsync(CancellationToken cancellationToken)
    {
        var invoices = await _dbContext.Invoices
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                Label = $"{x.InvoiceNumber} - Balance: {x.BalanceDue:N2}"
            })
            .ToListAsync(cancellationToken);

        ViewBag.Invoices = new SelectList(invoices, "Id", "Label");
    }
}

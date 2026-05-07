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

[Authorize]
[RequireModule(SystemModule.InvoiceGeneration)]
public class InvoicesController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IInvoiceNumberService _invoiceNumberService;
    private readonly IAuditService _auditService;

    public InvoicesController(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IInvoiceNumberService invoiceNumberService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _invoiceNumberService = invoiceNumberService;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Invoice> accessQuery = _dbContext.Invoices
            .AsNoTracking();

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId))
        {
            accessQuery = accessQuery.Where(x => x.ClientAccountId == clientId);
        }

        int draftCount = await accessQuery
            .CountAsync(x => x.Status == DomainValues.InvoiceStatus.Draft, cancellationToken);

        int issuedCount = await accessQuery
            .CountAsync(x => x.Status == DomainValues.InvoiceStatus.Issued, cancellationToken);

        int partiallyPaidCount = await accessQuery
            .CountAsync(x => x.Status == DomainValues.InvoiceStatus.PartiallyPaid, cancellationToken);

        int paidCount = await accessQuery
            .CountAsync(x => x.Status == DomainValues.InvoiceStatus.Paid, cancellationToken);

        DateTime utcNow = DateTime.UtcNow;
        int overdueCount = await accessQuery
            .CountAsync(x =>
                x.BalanceDue > 0
                && x.DueDateUtc < utcNow
                && x.Status != DomainValues.InvoiceStatus.Paid
                && x.Status != DomainValues.InvoiceStatus.Cancelled,
                cancellationToken);

        string searchFilter = search?.Trim() ?? string.Empty;
        string statusFilter = status?.Trim() ?? string.Empty;

        IQueryable<Invoice> filteredQuery = accessQuery
            .Include(x => x.ClientAccount);

        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            filteredQuery = filteredQuery.Where(x =>
                x.InvoiceNumber.Contains(searchFilter)
                || x.Status.Contains(searchFilter)
                || (x.ClientAccount != null && x.ClientAccount.CompanyName.Contains(searchFilter)));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && !string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            filteredQuery = filteredQuery.Where(x => x.Status == statusFilter);
        }

        int normalizedPageSize = Math.Clamp(pageSize, 10, 100);
        int totalCount = await filteredQuery.CountAsync(cancellationToken);
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        int normalizedPage = Math.Clamp(page, 1, totalPages);

        var invoices = await filteredQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var model = new InvoiceIndexViewModel
        {
            Search = searchFilter,
            Status = statusFilter,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            DraftCount = draftCount,
            IssuedCount = issuedCount,
            PartiallyPaidCount = partiallyPaidCount,
            PaidCount = paidCount,
            OverdueCount = overdueCount,
            Invoices = invoices
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.ClientAccount)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.Adjustments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId) && invoice.ClientAccountId != clientId)
        {
            return Forbid();
        }

        return View(invoice);
    }

    [Authorize(Policy = AuthorizationPolicies.BillingOperations)]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateClientsAsync(cancellationToken);
        return View(new InvoiceCreateViewModel());
    }

    [Authorize(Policy = AuthorizationPolicies.BillingOperations)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceCreateViewModel model, CancellationToken cancellationToken)
    {
        model.Items = model.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Description) && x.Quantity > 0 && x.UnitPrice > 0)
            .ToList();

        if (model.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "At least one invoice line item is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateClientsAsync(cancellationToken);
            return View(model);
        }

        int createdBy = _currentUser.UserId ?? 0;
        var invoice = new Invoice
        {
            InvoiceNumber = await _invoiceNumberService.GenerateAsync(cancellationToken),
            ClientAccountId = model.ClientAccountId,
            CreatedByUserId = createdBy,
            IssueDateUtc = DateTime.UtcNow,
            DueDateUtc = model.DueDateUtc,
            TaxRate = model.TaxRate,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? string.Empty : model.Notes.Trim()
        };

        foreach (InvoiceItemInputModel item in model.Items)
        {
            invoice.Items.Add(new InvoiceLineItem
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = Math.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero)
            });
        }

        InvoiceCalculator.RecalculateTotals(invoice);

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "CREATE",
            "Invoice",
            invoice.Id.ToString(),
            $"Created {invoice.InvoiceNumber}",
            cancellationToken);

        TempData["Success"] = "Invoice created successfully.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [Authorize(Policy = AuthorizationPolicies.InvoiceCancellation)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status == DomainValues.InvoiceStatus.Paid)
        {
            TempData["Error"] = "Paid invoices cannot be cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (invoice.Status == DomainValues.InvoiceStatus.Cancelled)
        {
            TempData["Error"] = "Invoice is already cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        invoice.Status = DomainValues.InvoiceStatus.Cancelled;
        invoice.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "CANCEL",
            "Invoice",
            invoice.Id.ToString(),
            $"Cancelled {invoice.InvoiceNumber}",
            cancellationToken);

        TempData["Success"] = "Invoice cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateClientsAsync(CancellationToken cancellationToken)
    {
        var clients = await _dbContext.Clients
            .AsNoTracking()
            .OrderBy(x => x.CompanyName)
            .ToListAsync(cancellationToken);

        ViewBag.Clients = new SelectList(clients, nameof(ClientAccount.Id), nameof(ClientAccount.CompanyName));
    }

    private bool TryGetClientAccountId(out int clientId)
    {
        string? claim = User.FindFirst("ClientAccountId")?.Value;
        return int.TryParse(claim, out clientId);
    }
}

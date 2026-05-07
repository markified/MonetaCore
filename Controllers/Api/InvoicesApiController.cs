using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.ApiModels;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.Services;

namespace MonetaCore.Controllers.Api;

[ApiController]
[Route("api/invoices")]
[Authorize]
[RequireModule(SystemModule.InvoiceGeneration)]
public class InvoicesApiController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IInvoiceNumberService _invoiceNumberService;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public InvoicesApiController(
        AppDbContext dbContext,
        IInvoiceNumberService invoiceNumberService,
        ICurrentUserService currentUser,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _invoiceNumberService = invoiceNumberService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApiInvoiceSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        IQueryable<Invoice> query = _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.ClientAccount)
            .OrderByDescending(x => x.CreatedAtUtc);

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId))
        {
            query = query.Where(x => x.ClientAccountId == clientId);
        }

        var results = await query
            .Select(x => new ApiInvoiceSummaryDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                ClientAccountId = x.ClientAccountId,
                ClientName = x.ClientAccount != null ? x.ClientAccount.CompanyName : "Unknown",
                IssueDateUtc = x.IssueDateUtc,
                DueDateUtc = x.DueDateUtc,
                Status = x.Status,
                TotalAmount = x.TotalAmount,
                BalanceDue = x.BalanceDue
            })
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiInvoiceDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.ClientAccount)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId) && invoice.ClientAccountId != clientId)
        {
            return Forbid();
        }

        return Ok(ToDetailDto(invoice));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.BillingOperations)]
    public async Task<ActionResult<ApiInvoiceDetailDto>> Create(ApiInvoiceCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "At least one invoice line item is required." });
        }

        bool clientExists = await _dbContext.Clients.AnyAsync(x => x.Id == request.ClientAccountId, cancellationToken);
        if (!clientExists)
        {
            return NotFound(new { message = "Client account not found." });
        }

        int createdBy = _currentUser.UserId ?? 0;
        var invoice = new Invoice
        {
            InvoiceNumber = await _invoiceNumberService.GenerateAsync(cancellationToken),
            ClientAccountId = request.ClientAccountId,
            CreatedByUserId = createdBy,
            IssueDateUtc = DateTime.UtcNow,
            DueDateUtc = request.DueDateUtc,
            TaxRate = request.TaxRate,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? string.Empty : request.Notes.Trim()
        };

        foreach (ApiInvoiceItemRequest item in request.Items)
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
            $"Created {invoice.InvoiceNumber} via API",
            cancellationToken);

        var created = await _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.ClientAccount)
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == invoice.Id, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToDetailDto(created));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.BillingOperations)]
    public async Task<ActionResult<ApiInvoiceDetailDto>> Update(int id, ApiInvoiceUpdateRequest request, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .Include(x => x.ClientAccount)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status == DomainValues.InvoiceStatus.Paid || invoice.Status == DomainValues.InvoiceStatus.Cancelled)
        {
            return BadRequest(new { message = "Paid or cancelled invoices cannot be edited." });
        }

        if (request.DueDateUtc.HasValue)
        {
            invoice.DueDateUtc = request.DueDateUtc.Value;
        }

        if (request.TaxRate.HasValue)
        {
            invoice.TaxRate = request.TaxRate.Value;
        }

        if (request.Notes is not null)
        {
            invoice.Notes = string.IsNullOrWhiteSpace(request.Notes) ? string.Empty : request.Notes.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!IsValidStatus(request.Status))
            {
                return BadRequest(new { message = "Invalid invoice status." });
            }

            invoice.Status = request.Status;
        }

        if (request.Items is not null)
        {
            if (request.Items.Count == 0)
            {
                return BadRequest(new { message = "At least one invoice line item is required." });
            }

            _dbContext.InvoiceItems.RemoveRange(invoice.Items);
            invoice.Items.Clear();

            foreach (ApiInvoiceItemRequest item in request.Items)
            {
                invoice.Items.Add(new InvoiceLineItem
                {
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = Math.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero)
                });
            }
        }

        InvoiceCalculator.RecalculateTotals(invoice);
        invoice.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "UPDATE",
            "Invoice",
            invoice.Id.ToString(),
            $"Updated {invoice.InvoiceNumber} via API",
            cancellationToken);

        return Ok(ToDetailDto(invoice));
    }

    private static ApiInvoiceDetailDto ToDetailDto(Invoice invoice)
    {
        return new ApiInvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            ClientAccountId = invoice.ClientAccountId,
            ClientName = invoice.ClientAccount?.CompanyName ?? "Unknown",
            IssueDateUtc = invoice.IssueDateUtc,
            DueDateUtc = invoice.DueDateUtc,
            Status = invoice.Status,
            TotalAmount = invoice.TotalAmount,
            BalanceDue = invoice.BalanceDue,
            TaxRate = invoice.TaxRate,
            Subtotal = invoice.Subtotal,
            TaxAmount = invoice.TaxAmount,
            AdjustmentTotal = invoice.AdjustmentTotal,
            AmountPaid = invoice.AmountPaid,
            Notes = invoice.Notes,
            Items = invoice.Items.Select(item => new ApiInvoiceLineItemDto
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal
            }).ToList()
        };
    }

    private static bool IsValidStatus(string status)
    {
        return status == DomainValues.InvoiceStatus.Draft
            || status == DomainValues.InvoiceStatus.Issued
            || status == DomainValues.InvoiceStatus.PartiallyPaid
            || status == DomainValues.InvoiceStatus.Paid
            || status == DomainValues.InvoiceStatus.Overdue
            || status == DomainValues.InvoiceStatus.Cancelled;
    }

    private bool TryGetClientAccountId(out int clientId)
    {
        string? claim = User.FindFirst("ClientAccountId")?.Value;
        return int.TryParse(claim, out clientId);
    }
}

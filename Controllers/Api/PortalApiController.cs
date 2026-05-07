using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.ApiModels;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Services;

namespace MonetaCore.Controllers.Api;

[ApiController]
[Route("api/portal")]
[Authorize]
[RequireModule(SystemModule.InvoiceGeneration, SystemModule.PaymentProcessing)]
public class PortalApiController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IEventOutboxService _eventOutboxService;

    public PortalApiController(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IAuditService auditService,
        IEventOutboxService eventOutboxService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _auditService = auditService;
        _eventOutboxService = eventOutboxService;
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<IReadOnlyList<PortalInvoiceSummaryDto>>> GetInvoices(CancellationToken cancellationToken)
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
            .Select(x => new PortalInvoiceSummaryDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
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

    [HttpGet("payments")]
    public async Task<ActionResult<IReadOnlyList<PortalPaymentSummaryDto>>> GetPayments(CancellationToken cancellationToken)
    {
        IQueryable<PaymentTransaction> query = _dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Invoice)
            .ThenInclude(x => x!.ClientAccount)
            .OrderByDescending(x => x.PaidAtUtc);

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId))
        {
            query = query.Where(x => x.Invoice!.ClientAccountId == clientId);
        }

        var results = await query
            .Select(x => new PortalPaymentSummaryDto
            {
                Id = x.Id,
                InvoiceId = x.InvoiceId,
                InvoiceNumber = x.Invoice != null ? x.Invoice.InvoiceNumber : string.Empty,
                Method = x.Method,
                Status = x.Status,
                Amount = x.Amount,
                ReferenceNumber = x.ReferenceNumber,
                PaidAtUtc = x.PaidAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    [HttpGet("receipts/{paymentId:int}")]
    public async Task<ActionResult<PortalReceiptDto>> GetReceipt(int paymentId, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Invoice)
            .ThenInclude(x => x!.ClientAccount)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        if (User.IsInRole(ApplicationRoles.Client)
            && TryGetClientAccountId(out int clientId)
            && payment.Invoice != null
            && payment.Invoice.ClientAccountId != clientId)
        {
            return Forbid();
        }

        var receipt = new PortalReceiptDto
        {
            PaymentId = payment.Id,
            ReceiptNumber = $"RCP-{payment.PaidAtUtc:yyyyMMdd}-{payment.Id}",
            InvoiceNumber = payment.Invoice?.InvoiceNumber ?? string.Empty,
            ClientName = payment.Invoice?.ClientAccount?.CompanyName ?? "Unknown",
            Method = payment.Method,
            Status = payment.Status,
            Amount = payment.Amount,
            PaidAtUtc = payment.PaidAtUtc,
            ReferenceNumber = payment.ReferenceNumber,
            Notes = payment.Notes
        };

        return Ok(receipt);
    }

    [HttpGet("disputes")]
    public async Task<ActionResult<IReadOnlyList<PortalDisputeSummaryDto>>> GetDisputes(CancellationToken cancellationToken)
    {
        IQueryable<PortalDispute> query = _dbContext.PortalDisputes
            .AsNoTracking()
            .Include(x => x.Invoice)
            .OrderByDescending(x => x.SubmittedAtUtc);

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId))
        {
            query = query.Where(x => x.Invoice != null && x.Invoice.ClientAccountId == clientId);
        }

        var results = await query
            .Select(x => new PortalDisputeSummaryDto
            {
                DisputeReference = x.DisputeReference,
                InvoiceId = x.InvoiceId,
                InvoiceNumber = x.Invoice != null ? x.Invoice.InvoiceNumber : string.Empty,
                Subject = x.Subject,
                Status = x.Status,
                SubmittedAtUtc = x.SubmittedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    [HttpPost("disputes")]
    public async Task<ActionResult<PortalDisputeResponseDto>> SubmitDispute(
        PortalDisputeCreateRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return NotFound(new { message = "Invoice not found." });
        }

        if (User.IsInRole(ApplicationRoles.Client)
            && TryGetClientAccountId(out int clientId)
            && invoice.ClientAccountId != clientId)
        {
            return Forbid();
        }

        DateTime submittedAt = DateTime.UtcNow;
        string disputeReference = $"DSP-{submittedAt:yyyyMMddHHmmss}-{request.InvoiceId}-{Guid.NewGuid():N}"[..40];

        var dispute = new PortalDispute
        {
            DisputeReference = disputeReference,
            InvoiceId = request.InvoiceId,
            SubmittedByUserId = _currentUser.UserId,
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            Status = DomainValues.DisputeStatus.Submitted,
            SubmittedAtUtc = submittedAt,
            UpdatedAtUtc = submittedAt
        };

        _dbContext.PortalDisputes.Add(dispute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "PORTAL_DISPUTE",
            "PortalDispute",
            dispute.Id.ToString(),
            $"{request.Subject}: {request.Message}",
            cancellationToken);

        await _eventOutboxService.QueueAsync(
            eventType: "PortalDisputeSubmitted",
            producer: nameof(PortalApiController),
            payload: new
            {
                DisputeReference = disputeReference,
                request.InvoiceId,
                request.Subject,
                request.Message,
                SubmittedByUserId = _currentUser.UserId,
                SubmittedByUserName = _currentUser.UserName,
                SubmittedAtUtc = submittedAt
            },
            correlationId: HttpContext.TraceIdentifier,
            cancellationToken: cancellationToken);

        var response = new PortalDisputeResponseDto
        {
            DisputeReference = disputeReference,
            InvoiceId = request.InvoiceId,
            SubmittedAtUtc = submittedAt,
            Status = "Submitted"
        };

        return Ok(response);
    }

    private bool TryGetClientAccountId(out int clientId)
    {
        string? claim = User.FindFirst("ClientAccountId")?.Value;
        return int.TryParse(claim, out clientId);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.ApiModels;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Services;
using MonetaCore.ViewModels;

namespace MonetaCore.Controllers.Api;

[ApiController]
[Route("api/payments")]
[Authorize]
[RequireModule(SystemModule.PaymentProcessing)]
public class PaymentsApiController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPayMongoService _payMongoService;

    public PaymentsApiController(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IAuditService auditService,
        IPayMongoService payMongoService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _auditService = auditService;
        _payMongoService = payMongoService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApiPaymentDto>>> GetAll(CancellationToken cancellationToken)
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
            .Select(x => new ApiPaymentDto
            {
                Id = x.Id,
                InvoiceId = x.InvoiceId,
                InvoiceNumber = x.Invoice != null ? x.Invoice.InvoiceNumber : string.Empty,
                ClientName = x.Invoice != null && x.Invoice.ClientAccount != null ? x.Invoice.ClientAccount.CompanyName : "Unknown",
                Amount = x.Amount,
                Method = x.Method,
                Status = x.Status,
                ReferenceNumber = x.ReferenceNumber,
                GatewayTransactionId = x.GatewayTransactionId,
                PaidAtUtc = x.PaidAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiPaymentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Invoice)
            .ThenInclude(x => x!.ClientAccount)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId)
            && payment.Invoice != null && payment.Invoice.ClientAccountId != clientId)
        {
            return Forbid();
        }

        return Ok(new ApiPaymentDto
        {
            Id = payment.Id,
            InvoiceId = payment.InvoiceId,
            InvoiceNumber = payment.Invoice != null ? payment.Invoice.InvoiceNumber : string.Empty,
            ClientName = payment.Invoice != null && payment.Invoice.ClientAccount != null ? payment.Invoice.ClientAccount.CompanyName : "Unknown",
            Amount = payment.Amount,
            Method = payment.Method,
            Status = payment.Status,
            ReferenceNumber = payment.ReferenceNumber,
            GatewayTransactionId = payment.GatewayTransactionId,
            PaidAtUtc = payment.PaidAtUtc
        });
    }

    [HttpPost]
    public async Task<ActionResult<ApiPaymentDto>> Create(ApiPaymentCreateRequest request, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .Include(x => x.ClientAccount)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return NotFound(new { message = "Invoice not found." });
        }

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId) && invoice.ClientAccountId != clientId)
        {
            return Forbid();
        }

        if (request.Amount > invoice.BalanceDue)
        {
            return BadRequest(new { message = "Payment cannot exceed remaining balance." });
        }

        if (string.IsNullOrWhiteSpace(request.PayMongoFlow))
        {
            request.PayMongoFlow = DomainValues.PayMongoFlow.Checkout;
        }

        if (request.Method == DomainValues.PaymentMethod.PayMongo
            && request.PayMongoFlow == DomainValues.PayMongoFlow.Card)
        {
            if (string.IsNullOrWhiteSpace(request.CardholderName)
                || string.IsNullOrWhiteSpace(request.CardNumber)
                || !request.CardExpMonth.HasValue
                || !request.CardExpYear.HasValue
                || string.IsNullOrWhiteSpace(request.CardCvc))
            {
                return BadRequest(new { message = "Card details are required for PayMongo card payments." });
            }
        }

        if (!IsAllowedMethod(request.Method))
        {
            return BadRequest(new { message = "Invalid payment method. Allowed methods are Cash and PayMongo." });
        }

        string paymentNotes = string.IsNullOrWhiteSpace(request.Notes) ? string.Empty : request.Notes.Trim();
        string nextActionUrl = string.Empty;

        if (request.Method == DomainValues.PaymentMethod.PayMongo)
        {
            var payment = new PaymentTransaction
            {
                InvoiceId = request.InvoiceId,
                Amount = request.Amount,
                Method = request.Method,
                ReferenceNumber = request.ReferenceNumber,
                Status = DomainValues.PaymentStatus.Pending,
                Notes = paymentNotes,
                ProcessedByUserId = _currentUser.UserId,
                PaidAtUtc = DateTime.UtcNow
            };

            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var payloadModel = new PaymentCreateViewModel
            {
                InvoiceId = request.InvoiceId,
                Amount = request.Amount,
                Method = request.Method,
                ReferenceNumber = request.ReferenceNumber,
                Notes = paymentNotes,
                PayMongoFlow = request.PayMongoFlow,
                CardholderName = request.CardholderName,
                CardNumber = request.CardNumber,
                CardExpMonth = request.CardExpMonth,
                CardExpYear = request.CardExpYear,
                CardCvc = request.CardCvc
            };

            string successUrl = Url.Action("PayMongoReturn", "Payments", new { paymentId = payment.Id, status = "success" }, Request.Scheme) ?? string.Empty;
            string cancelUrl = Url.Action("PayMongoReturn", "Payments", new { paymentId = payment.Id, status = "cancel" }, Request.Scheme) ?? string.Empty;

            PayMongoPaymentResult result = request.PayMongoFlow == DomainValues.PayMongoFlow.Card
                ? await _payMongoService.CreateCardPaymentAsync(payloadModel, payment.Id, invoice, successUrl, cancellationToken)
                : await _payMongoService.CreateCheckoutSessionAsync(payloadModel, payment.Id, invoice, successUrl, cancelUrl, cancellationToken);

            if (!result.IsSuccess)
            {
                _dbContext.Payments.Remove(payment);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return BadRequest(new { message = result.Message });
            }

            payment.GatewayTransactionId = result.TransactionId;
            payment.Status = result.PaymentStatus;
            payment.Notes = string.IsNullOrWhiteSpace(paymentNotes) ? result.Message : $"{paymentNotes} | {result.Message}";
            nextActionUrl = result.RedirectUrl;

            if (result.PaymentStatus == DomainValues.PaymentStatus.Completed)
            {
                payment.PaidAtUtc = DateTime.UtcNow;
                invoice.AmountPaid += request.Amount;
                InvoiceCalculator.RecalculateTotals(invoice);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                _currentUser.UserId,
                _currentUser.UserName,
                result.PaymentStatus == DomainValues.PaymentStatus.Completed ? "PAYMENT" : "PAYMENT_PENDING",
                "Invoice",
                invoice.Id.ToString(),
                $"Paid {request.Amount:N2} via {request.Method} ({request.PayMongoFlow}) (API)",
                cancellationToken);

            var paymongoDto = new ApiPaymentDto
            {
                Id = payment.Id,
                InvoiceId = payment.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                ClientName = invoice.ClientAccount?.CompanyName ?? "Unknown",
                Amount = payment.Amount,
                Method = payment.Method,
                Status = payment.Status,
                ReferenceNumber = payment.ReferenceNumber,
                GatewayTransactionId = payment.GatewayTransactionId,
                PaidAtUtc = payment.PaidAtUtc,
                NextActionUrl = nextActionUrl
            };

            return CreatedAtAction(nameof(GetById), new { id = payment.Id }, paymongoDto);
        }

        var manualPayment = new PaymentTransaction
        {
            InvoiceId = request.InvoiceId,
            Amount = request.Amount,
            Method = request.Method,
            ReferenceNumber = request.ReferenceNumber,
            GatewayTransactionId = string.Empty,
            Status = DomainValues.PaymentStatus.Completed,
            Notes = paymentNotes,
            ProcessedByUserId = _currentUser.UserId,
            PaidAtUtc = DateTime.UtcNow
        };

        _dbContext.Payments.Add(manualPayment);

        invoice.AmountPaid += request.Amount;
        InvoiceCalculator.RecalculateTotals(invoice);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "PAYMENT",
            "Invoice",
            invoice.Id.ToString(),
            $"Paid {request.Amount:N2} via {request.Method} (API)",
            cancellationToken);

        var dto = new ApiPaymentDto
        {
            Id = manualPayment.Id,
            InvoiceId = manualPayment.InvoiceId,
            InvoiceNumber = invoice.InvoiceNumber,
            ClientName = invoice.ClientAccount?.CompanyName ?? "Unknown",
            Amount = manualPayment.Amount,
            Method = manualPayment.Method,
            Status = manualPayment.Status,
            ReferenceNumber = manualPayment.ReferenceNumber,
            GatewayTransactionId = manualPayment.GatewayTransactionId,
            PaidAtUtc = manualPayment.PaidAtUtc,
            NextActionUrl = nextActionUrl
        };

        return CreatedAtAction(nameof(GetById), new { id = manualPayment.Id }, dto);
    }

    private static bool IsAllowedMethod(string method)
    {
        return string.Equals(method, DomainValues.PaymentMethod.Cash, StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, DomainValues.PaymentMethod.PayMongo, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetClientAccountId(out int clientId)
    {
        string? claim = User.FindFirst("ClientAccountId")?.Value;
        return int.TryParse(claim, out clientId);
    }
}

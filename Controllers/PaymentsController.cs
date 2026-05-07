using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Services;
using MonetaCore.ViewModels;

namespace MonetaCore.Controllers;

[Authorize]
[RequireModule(SystemModule.PaymentProcessing)]
public class PaymentsController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPayMongoService _payMongoService;

    public PaymentsController(
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

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        IQueryable<PaymentTransaction> query = _dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Invoice)
            .ThenInclude(x => x!.ClientAccount)
            .Include(x => x.ProcessedByUser)
            .OrderByDescending(x => x.PaidAtUtc);

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId))
        {
            query = query.Where(x => x.Invoice!.ClientAccountId == clientId);
        }

        var payments = await query.ToListAsync(cancellationToken);
        return View(payments);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? invoiceId, CancellationToken cancellationToken)
    {
        await PopulateInvoiceDropDownAsync(cancellationToken);

        var model = new PaymentCreateViewModel();
        if (invoiceId.HasValue)
        {
            var invoice = await _dbContext.Invoices
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == invoiceId.Value, cancellationToken);

            if (invoice != null)
            {
                model.InvoiceId = invoice.Id;
                model.Amount = invoice.BalanceDue;
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentCreateViewModel model, CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .Include(x => x.ClientAccount)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == model.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            ModelState.AddModelError(nameof(model.InvoiceId), "Invoice not found.");
        }

        if (invoice != null && User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId) && invoice.ClientAccountId != clientId)
        {
            return Forbid();
        }

        if (invoice != null && model.Amount > invoice.BalanceDue)
        {
            ModelState.AddModelError(nameof(model.Amount), "Payment cannot exceed remaining balance.");
        }

        if (!IsAllowedMethod(model.Method))
        {
            ModelState.AddModelError(nameof(model.Method), "Invalid payment method. Allowed methods are Cash and PayMongo.");
        }

        if (string.IsNullOrWhiteSpace(model.PayMongoFlow))
        {
            model.PayMongoFlow = DomainValues.PayMongoFlow.Checkout;
        }

        if (model.Method == DomainValues.PaymentMethod.PayMongo)
        {
            string normalizedFlow = model.PayMongoFlow.Trim();
            if (!string.Equals(normalizedFlow, DomainValues.PayMongoFlow.Card, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedFlow, DomainValues.PayMongoFlow.Checkout, StringComparison.OrdinalIgnoreCase))
            {
                normalizedFlow = DomainValues.PayMongoFlow.Checkout;
            }

            model.PayMongoFlow = string.Equals(normalizedFlow, DomainValues.PayMongoFlow.Card, StringComparison.OrdinalIgnoreCase)
                ? DomainValues.PayMongoFlow.Card
                : DomainValues.PayMongoFlow.Checkout;

            bool hasAnyCardInput = !string.IsNullOrWhiteSpace(model.CardholderName)
                || !string.IsNullOrWhiteSpace(model.CardNumber)
                || model.CardExpMonth.HasValue
                || model.CardExpYear.HasValue
                || !string.IsNullOrWhiteSpace(model.CardCvc);

            if (model.PayMongoFlow == DomainValues.PayMongoFlow.Card && !hasAnyCardInput)
            {
                model.PayMongoFlow = DomainValues.PayMongoFlow.Checkout;
            }
        }

        if (model.Method == DomainValues.PaymentMethod.PayMongo
            && model.PayMongoFlow == DomainValues.PayMongoFlow.Card)
        {
            if (string.IsNullOrWhiteSpace(model.CardholderName))
            {
                ModelState.AddModelError(nameof(model.CardholderName), "Cardholder name is required for card payments.");
            }

            if (string.IsNullOrWhiteSpace(model.CardNumber))
            {
                ModelState.AddModelError(nameof(model.CardNumber), "Card number is required for card payments.");
            }

            if (!model.CardExpMonth.HasValue)
            {
                ModelState.AddModelError(nameof(model.CardExpMonth), "Expiry month is required for card payments.");
            }

            if (!model.CardExpYear.HasValue)
            {
                ModelState.AddModelError(nameof(model.CardExpYear), "Expiry year is required for card payments.");
            }

            if (string.IsNullOrWhiteSpace(model.CardCvc))
            {
                ModelState.AddModelError(nameof(model.CardCvc), "CVC is required for card payments.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateInvoiceDropDownAsync(cancellationToken);
            return View(model);
        }

        string paymentNotes = string.IsNullOrWhiteSpace(model.Notes) ? string.Empty : model.Notes.Trim();

        if (model.Method == DomainValues.PaymentMethod.PayMongo)
        {
            var payment = new PaymentTransaction
            {
                InvoiceId = model.InvoiceId,
                Amount = model.Amount,
                Method = model.Method,
                ReferenceNumber = model.ReferenceNumber,
                Status = DomainValues.PaymentStatus.Pending,
                Notes = paymentNotes,
                ProcessedByUserId = _currentUser.UserId,
                PaidAtUtc = DateTime.UtcNow
            };

            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            string successUrl = Url.Action(nameof(PayMongoReturn), "Payments", new { paymentId = payment.Id, status = "success" }, Request.Scheme) ?? string.Empty;
            string cancelUrl = Url.Action(nameof(PayMongoReturn), "Payments", new { paymentId = payment.Id, status = "cancel" }, Request.Scheme) ?? string.Empty;

            PayMongoPaymentResult result = model.PayMongoFlow == DomainValues.PayMongoFlow.Card
                ? await _payMongoService.CreateCardPaymentAsync(model, payment.Id, invoice!, successUrl, cancellationToken)
                : await _payMongoService.CreateCheckoutSessionAsync(model, payment.Id, invoice!, successUrl, cancelUrl, cancellationToken);

            if (!result.IsSuccess)
            {
                _dbContext.Payments.Remove(payment);
                await _dbContext.SaveChangesAsync(cancellationToken);
                ModelState.AddModelError(string.Empty, result.Message);
                await PopulateInvoiceDropDownAsync(cancellationToken);
                return View(model);
            }

            payment.GatewayTransactionId = result.TransactionId;
            payment.Status = result.PaymentStatus;
            payment.Notes = string.IsNullOrWhiteSpace(paymentNotes) ? result.Message : $"{paymentNotes} | {result.Message}";

            if (result.PaymentStatus == DomainValues.PaymentStatus.Completed)
            {
                payment.PaidAtUtc = DateTime.UtcNow;
                invoice!.AmountPaid += model.Amount;
                InvoiceCalculator.RecalculateTotals(invoice);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                _currentUser.UserId,
                _currentUser.UserName,
                result.PaymentStatus == DomainValues.PaymentStatus.Completed ? "PAYMENT" : "PAYMENT_PENDING",
                "Invoice",
                invoice!.Id.ToString(),
                $"Paid {model.Amount:N2} via {model.Method} ({model.PayMongoFlow})",
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.RedirectUrl))
            {
                return Redirect(result.RedirectUrl);
            }

            TempData["Success"] = result.PaymentStatus == DomainValues.PaymentStatus.Completed
                ? "PayMongo payment completed successfully."
                : "PayMongo payment initiated. Complete the payment in PayMongo.";

            return RedirectToAction("Details", "Invoices", new { id = model.InvoiceId });
        }

        var manualPayment = new PaymentTransaction
        {
            InvoiceId = model.InvoiceId,
            Amount = model.Amount,
            Method = model.Method,
            ReferenceNumber = model.ReferenceNumber,
            GatewayTransactionId = string.Empty,
            Status = DomainValues.PaymentStatus.Completed,
            Notes = paymentNotes,
            ProcessedByUserId = _currentUser.UserId,
            PaidAtUtc = DateTime.UtcNow
        };

        _dbContext.Payments.Add(manualPayment);

        invoice!.AmountPaid += model.Amount;
        InvoiceCalculator.RecalculateTotals(invoice);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "PAYMENT",
            "Invoice",
            invoice.Id.ToString(),
            $"Paid {model.Amount:N2} via {model.Method}",
            cancellationToken);

        TempData["Success"] = "Payment recorded successfully.";
        return RedirectToAction("Details", "Invoices", new { id = model.InvoiceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayMongo(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments
            .Include(x => x.Invoice)
            .ThenInclude(x => x!.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (payment is null)
        {
            TempData["Error"] = "Payment not found.";
            return RedirectToAction(nameof(Index));
        }

        if (payment.Method != DomainValues.PaymentMethod.PayMongo)
        {
            TempData["Error"] = "Only PayMongo payments can be confirmed manually.";
            return RedirectToAction("Details", "Invoices", new { id = payment.InvoiceId });
        }

        if (payment.Status == DomainValues.PaymentStatus.Completed)
        {
            TempData["Success"] = "Payment is already completed.";
            return RedirectToAction("Details", "Invoices", new { id = payment.InvoiceId });
        }

        if (payment.Status != DomainValues.PaymentStatus.Pending)
        {
            TempData["Error"] = "Only pending PayMongo payments can be confirmed.";
            return RedirectToAction("Details", "Invoices", new { id = payment.InvoiceId });
        }

        payment.Status = DomainValues.PaymentStatus.Completed;
        payment.PaidAtUtc = DateTime.UtcNow;

        if (payment.Invoice != null)
        {
            payment.Invoice.AmountPaid += payment.Amount;
            InvoiceCalculator.RecalculateTotals(payment.Invoice);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "PAYMENT_CONFIRM",
            "Invoice",
            payment.InvoiceId.ToString(),
            $"Manually confirmed PayMongo payment {payment.Id}.",
            cancellationToken);

        TempData["Success"] = "PayMongo payment confirmed manually.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Details", "Invoices", new { id = payment.InvoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> PayMongoReturn(int paymentId, string status, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments
            .Include(x => x.Invoice)
            .ThenInclude(x => x!.Items)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            TempData["Error"] = "Payment not found.";
            return RedirectToAction(nameof(Index));
        }

        bool isSuccess = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
        bool isCancel = string.Equals(status, "cancel", StringComparison.OrdinalIgnoreCase);

        if (isSuccess && payment.Status != DomainValues.PaymentStatus.Completed)
        {
            payment.Status = DomainValues.PaymentStatus.Completed;
            payment.PaidAtUtc = DateTime.UtcNow;

            if (payment.Invoice != null)
            {
                payment.Invoice.AmountPaid += payment.Amount;
                InvoiceCalculator.RecalculateTotals(payment.Invoice);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "PayMongo payment completed successfully.";
        }
        else if (isCancel && payment.Status != DomainValues.PaymentStatus.Completed)
        {
            payment.Status = DomainValues.PaymentStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["Error"] = "PayMongo checkout was cancelled.";
        }
        else
        {
            TempData["Success"] = "PayMongo payment status updated.";
        }

        return RedirectToAction("Details", "Invoices", new { id = payment.InvoiceId });
    }

    private async Task PopulateInvoiceDropDownAsync(CancellationToken cancellationToken)
    {
        IQueryable<Invoice> query = _dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.BalanceDue > 0)
            .OrderByDescending(x => x.DueDateUtc);

        if (User.IsInRole(ApplicationRoles.Client) && TryGetClientAccountId(out int clientId))
        {
            query = query.Where(x => x.ClientAccountId == clientId);
        }

        var invoices = await query
            .Select(x => new
            {
                x.Id,
                Label = $"{x.InvoiceNumber} - Balance: {x.BalanceDue:N2}"
            })
            .ToListAsync(cancellationToken);

        ViewBag.Invoices = new SelectList(invoices, "Id", "Label");
        ViewBag.Methods = new SelectList(new[]
        {
            DomainValues.PaymentMethod.Cash,
            DomainValues.PaymentMethod.PayMongo
        });
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

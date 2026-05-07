using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonetaCore.ApiModels;
using MonetaCore.Filters;
using MonetaCore.Services;

namespace MonetaCore.Controllers.Api;

[ApiController]
[Route("api/compliance")]
[Authorize]
[RequireModule(SystemModule.InvoiceGeneration, SystemModule.PaymentProcessing)]
public class ComplianceApiController : ControllerBase
{
    private readonly IComplianceService _complianceService;
    private readonly IEventOutboxService _eventOutboxService;
    private readonly ICurrentUserService _currentUser;

    public ComplianceApiController(
        IComplianceService complianceService,
        IEventOutboxService eventOutboxService,
        ICurrentUserService currentUser)
    {
        _complianceService = complianceService;
        _eventOutboxService = eventOutboxService;
        _currentUser = currentUser;
    }

    [HttpPost("tax/calculate")]
    public async Task<ActionResult<ComplianceTaxCalculationResponse>> CalculateTax(
        ComplianceTaxCalculationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _complianceService.CalculateTaxAsync(request, cancellationToken);

        await _eventOutboxService.QueueAsync(
            eventType: "ComplianceTaxCalculated",
            producer: nameof(ComplianceApiController),
            payload: new
            {
                request.Jurisdiction,
                request.CurrencyCode,
                request.TaxCode,
                request.TaxableAmount,
                result.AppliedTaxRate,
                result.TaxAmount,
                result.TotalAmount,
                UserId = _currentUser.UserId,
                UserName = _currentUser.UserName
            },
            correlationId: HttpContext.TraceIdentifier,
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    [HttpGet("tax/rules")]
    public async Task<ActionResult<IReadOnlyList<ComplianceTaxRuleDto>>> GetTaxRules(
        [FromQuery] string? jurisdiction,
        CancellationToken cancellationToken)
    {
        var rules = await _complianceService.GetTaxRulesAsync(jurisdiction, cancellationToken);
        return Ok(rules);
    }

    [HttpGet("currency/rates")]
    public async Task<ActionResult<IReadOnlyList<ComplianceCurrencyRateDto>>> GetCurrencyRates(
        [FromQuery] string? baseCurrency,
        CancellationToken cancellationToken)
    {
        var rates = await _complianceService.GetCurrencyRatesAsync(baseCurrency, cancellationToken);
        return Ok(rates);
    }
}

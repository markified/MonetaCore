using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MonetaCore.ApiModels;
using MonetaCore.Controllers.Api;
using MonetaCore.Services;

namespace MonetaCore.Tests;

public class ComplianceApiControllerTests
{
    [Fact]
    public async Task CalculateTax_ReturnsOkAndQueuesOutboxEvent()
    {
        var complianceOptions = Options.Create(new ComplianceOptions
        {
            DefaultTaxRate = 0.12m,
            TaxRules =
            [
                new ComplianceTaxRuleOption
                {
                    Jurisdiction = "PH",
                    TaxCode = "STANDARD",
                    TaxRate = 0.12m,
                    Description = "VAT"
                }
            ]
        });

        var complianceService = new ComplianceService(complianceOptions);
        var outbox = new FakeEventOutboxService();
        var currentUser = new FakeCurrentUserService
        {
            UserId = 11,
            UserName = "qa@local"
        };

        var controller = new ComplianceApiController(complianceService, outbox, currentUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "trace-123"
                }
            }
        };

        var request = new ComplianceTaxCalculationRequest
        {
            Jurisdiction = "PH",
            CurrencyCode = "PHP",
            TaxCode = "STANDARD",
            TaxableAmount = 1000m
        };

        ActionResult<ComplianceTaxCalculationResponse> actionResult = await controller.CalculateTax(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<ComplianceTaxCalculationResponse>(ok.Value);

        Assert.Equal(120m, payload.TaxAmount);
        Assert.Equal(1120m, payload.TotalAmount);
        Assert.Single(outbox.Envelopes);
        Assert.Equal("ComplianceTaxCalculated", outbox.Envelopes[0].EventType);
    }

    [Fact]
    public async Task GetCurrencyRates_ReturnsOk()
    {
        var complianceOptions = Options.Create(new ComplianceOptions
        {
            CurrencyRates =
            [
                new ComplianceCurrencyRateOption
                {
                    BaseCurrency = "PHP",
                    QuoteCurrency = "USD",
                    Rate = 0.0180m,
                    Source = "Test"
                }
            ]
        });

        var complianceService = new ComplianceService(complianceOptions);
        var outbox = new FakeEventOutboxService();
        var currentUser = new FakeCurrentUserService();

        var controller = new ComplianceApiController(complianceService, outbox, currentUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        ActionResult<IReadOnlyList<ComplianceCurrencyRateDto>> actionResult = await controller.GetCurrencyRates("PHP", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ComplianceCurrencyRateDto>>(ok.Value);

        Assert.Single(payload);
        Assert.Equal("USD", payload[0].QuoteCurrency);
    }
}

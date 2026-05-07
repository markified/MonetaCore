using MonetaCore.ApiModels;

namespace MonetaCore.Services;

public interface IComplianceService
{
    Task<ComplianceTaxCalculationResponse> CalculateTaxAsync(
        ComplianceTaxCalculationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplianceTaxRuleDto>> GetTaxRulesAsync(
        string? jurisdiction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplianceCurrencyRateDto>> GetCurrencyRatesAsync(
        string? baseCurrency,
        CancellationToken cancellationToken = default);
}

using Microsoft.Extensions.Options;
using MonetaCore.ApiModels;

namespace MonetaCore.Services;

public class ComplianceOptions
{
    public decimal DefaultTaxRate { get; set; } = 0.12m;

    public List<ComplianceTaxRuleOption> TaxRules { get; set; } =
    [
        new ComplianceTaxRuleOption
        {
            Jurisdiction = "PH",
            TaxCode = "STANDARD",
            TaxRate = 0.12m,
            Description = "Philippines VAT"
        },
        new ComplianceTaxRuleOption
        {
            Jurisdiction = "PH",
            TaxCode = "ZERO",
            TaxRate = 0m,
            Description = "Zero-rated transactions"
        }
    ];

    public List<ComplianceCurrencyRateOption> CurrencyRates { get; set; } =
    [
        new ComplianceCurrencyRateOption
        {
            BaseCurrency = "PHP",
            QuoteCurrency = "USD",
            Rate = 0.0178m,
            Source = "Configured"
        },
        new ComplianceCurrencyRateOption
        {
            BaseCurrency = "PHP",
            QuoteCurrency = "EUR",
            Rate = 0.0155m,
            Source = "Configured"
        },
        new ComplianceCurrencyRateOption
        {
            BaseCurrency = "USD",
            QuoteCurrency = "PHP",
            Rate = 56.2000m,
            Source = "Configured"
        }
    ];
}

public class ComplianceTaxRuleOption
{
    public string Jurisdiction { get; set; } = "PH";
    public string TaxCode { get; set; } = "STANDARD";
    public decimal TaxRate { get; set; } = 0.12m;
    public string Description { get; set; } = string.Empty;
}

public class ComplianceCurrencyRateOption
{
    public string BaseCurrency { get; set; } = "PHP";
    public string QuoteCurrency { get; set; } = "USD";
    public decimal Rate { get; set; } = 1m;
    public string Source { get; set; } = "Configured";
}

public class ComplianceService : IComplianceService
{
    private readonly ComplianceOptions _options;

    public ComplianceService(IOptions<ComplianceOptions> options)
    {
        _options = options.Value;
    }

    public Task<ComplianceTaxCalculationResponse> CalculateTaxAsync(
        ComplianceTaxCalculationRequest request,
        CancellationToken cancellationToken = default)
    {
        string jurisdiction = NormalizeOrDefault(request.Jurisdiction, "PH");
        string currencyCode = NormalizeOrDefault(request.CurrencyCode, "PHP");
        string taxCode = NormalizeOrDefault(request.TaxCode, "STANDARD");

        decimal appliedTaxRate = request.OverrideTaxRate
            ?? _options.TaxRules
                .FirstOrDefault(x =>
                    string.Equals(x.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.TaxCode, taxCode, StringComparison.OrdinalIgnoreCase))
                ?.TaxRate
            ?? _options.DefaultTaxRate;

        decimal taxable = request.TaxableAmount;
        decimal taxAmount = Math.Round(taxable * appliedTaxRate, 2, MidpointRounding.AwayFromZero);
        decimal total = taxable + taxAmount;

        var response = new ComplianceTaxCalculationResponse
        {
            Jurisdiction = jurisdiction,
            CurrencyCode = currencyCode,
            TaxCode = taxCode,
            AppliedTaxRate = appliedTaxRate,
            TaxableAmount = taxable,
            TaxAmount = taxAmount,
            TotalAmount = total,
            CalculatedAtUtc = DateTime.UtcNow
        };

        return Task.FromResult(response);
    }

    public Task<IReadOnlyList<ComplianceTaxRuleDto>> GetTaxRulesAsync(
        string? jurisdiction,
        CancellationToken cancellationToken = default)
    {
        string normalizedJurisdiction = NormalizeOrDefault(jurisdiction, string.Empty);

        IEnumerable<ComplianceTaxRuleOption> source = _options.TaxRules;
        if (!string.IsNullOrWhiteSpace(normalizedJurisdiction))
        {
            source = source.Where(x => string.Equals(x.Jurisdiction, normalizedJurisdiction, StringComparison.OrdinalIgnoreCase));
        }

        IReadOnlyList<ComplianceTaxRuleDto> results = source
            .Select(x => new ComplianceTaxRuleDto
            {
                Jurisdiction = x.Jurisdiction,
                TaxCode = x.TaxCode,
                TaxRate = x.TaxRate,
                Description = x.Description
            })
            .OrderBy(x => x.Jurisdiction)
            .ThenBy(x => x.TaxCode)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<ComplianceCurrencyRateDto>> GetCurrencyRatesAsync(
        string? baseCurrency,
        CancellationToken cancellationToken = default)
    {
        string normalizedBaseCurrency = NormalizeOrDefault(baseCurrency, "PHP");

        var configured = _options.CurrencyRates
            .Where(x => string.Equals(x.BaseCurrency, normalizedBaseCurrency, StringComparison.OrdinalIgnoreCase))
            .Select(x => new ComplianceCurrencyRateDto
            {
                BaseCurrency = x.BaseCurrency.ToUpperInvariant(),
                QuoteCurrency = x.QuoteCurrency.ToUpperInvariant(),
                Rate = x.Rate,
                EffectiveAtUtc = DateTime.UtcNow,
                Source = x.Source
            })
            .ToList();

        if (configured.Count == 0)
        {
            configured.Add(new ComplianceCurrencyRateDto
            {
                BaseCurrency = normalizedBaseCurrency,
                QuoteCurrency = normalizedBaseCurrency,
                Rate = 1m,
                EffectiveAtUtc = DateTime.UtcNow,
                Source = "Default"
            });
        }

        return Task.FromResult<IReadOnlyList<ComplianceCurrencyRateDto>>(configured);
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToUpperInvariant();
    }
}

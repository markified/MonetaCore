using System.ComponentModel.DataAnnotations;

namespace MonetaCore.ApiModels;

public class ComplianceTaxCalculationRequest
{
    [Required, StringLength(10)]
    public string Jurisdiction { get; set; } = "PH";

    [Required, StringLength(12)]
    public string CurrencyCode { get; set; } = "PHP";

    [StringLength(40)]
    public string TaxCode { get; set; } = "STANDARD";

    [Range(0, 999999999)]
    public decimal TaxableAmount { get; set; }

    [Range(0, 1)]
    public decimal? OverrideTaxRate { get; set; }
}

public class ComplianceTaxCalculationResponse
{
    public string Jurisdiction { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public decimal AppliedTaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ComplianceTaxRuleDto
{
    public string Jurisdiction { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ComplianceCurrencyRateDto
{
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime EffectiveAtUtc { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "Configured";
}

using System.ComponentModel.DataAnnotations;
using MonetaCore.Models;

namespace MonetaCore.ViewModels;

public class SystemConfigurationViewModel
{
    [Display(Name = "Manage Users & Roles")]
    public bool ManageUsersAndRolesEnabled { get; set; } = true;

    [Display(Name = "Invoice Generation & Tracking")]
    public bool InvoiceGenerationEnabled { get; set; } = true;

    [Display(Name = "Payment Processing")]
    public bool PaymentProcessingEnabled { get; set; } = true;

    [Display(Name = "Credit & Debit Management")]
    public bool CreditDebitManagementEnabled { get; set; } = true;

    [Display(Name = "Revenue Monitoring")]
    public bool RevenueMonitoringEnabled { get; set; } = true;

    [Display(Name = "Account Integration")]
    public bool AccountIntegrationEnabled { get; set; } = true;

    [Display(Name = "View Reports & Dashboards")]
    public bool ViewReportsDashboardsEnabled { get; set; } = true;

    [Display(Name = "View Audit Logs")]
    public bool ViewAuditLogsEnabled { get; set; } = true;

    [Display(Name = "Maintenance Mode")]
    public bool MaintenanceModeEnabled { get; set; }

    [Range(30, 1440, ErrorMessage = "Session timeout must be between 30 and 1440 minutes.")]
    [Display(Name = "Session Timeout (minutes)")]
    public int SessionTimeoutMinutes { get; set; } = 480;

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Revenue alert threshold must be a valid positive amount.")]
    [Display(Name = "Revenue Alert Threshold")]
    public decimal RevenueAlertThreshold { get; set; } = 100000m;

    [Display(Name = "Current PayMongo Secret Key")]
    public string MaskedPayMongoSecretKey { get; set; } = "Not set";

    [StringLength(240)]
    [DataType(DataType.Password)]
    [Display(Name = "Rotate PayMongo Secret Key")]
    public string NewPayMongoSecretKey { get; set; } = string.Empty;

    [Display(Name = "Clear PayMongo Secret Key")]
    public bool ClearPayMongoSecretKey { get; set; }

    [Url]
    [StringLength(260)]
    [Display(Name = "Accounting API Base URL")]
    public string AccountingApiBaseUrl { get; set; } = string.Empty;

    [Url]
    [StringLength(260)]
    [Display(Name = "Outbox Events API URL")]
    public string OutboxEventsApiUrl { get; set; } = string.Empty;

    [RegularExpression("None|ApiKey|Bearer", ErrorMessage = "Auth mode must be None, ApiKey, or Bearer.")]
    [StringLength(20)]
    [Display(Name = "Integration Auth Mode")]
    public string IntegrationAuthMode { get; set; } = DomainValues.IntegrationAuthMode.None;

    [StringLength(80)]
    [Display(Name = "Integration API Key Header")]
    public string IntegrationApiKeyHeaderName { get; set; } = "X-Api-Key";

    [Display(Name = "Current Integration API Key")]
    public string MaskedIntegrationApiKey { get; set; } = "Not set";

    [StringLength(240)]
    [DataType(DataType.Password)]
    [Display(Name = "Rotate Integration API Key")]
    public string NewIntegrationApiKey { get; set; } = string.Empty;

    [Display(Name = "Clear Integration API Key")]
    public bool ClearIntegrationApiKey { get; set; }

    [Display(Name = "Current Integration Bearer Token")]
    public string MaskedIntegrationBearerToken { get; set; } = "Not set";

    [StringLength(320)]
    [DataType(DataType.Password)]
    [Display(Name = "Rotate Integration Bearer Token")]
    public string NewIntegrationBearerToken { get; set; } = string.Empty;

    [Display(Name = "Clear Integration Bearer Token")]
    public bool ClearIntegrationBearerToken { get; set; }

    [Display(Name = "Auto Sync Enabled")]
    public bool AutoSyncEnabled { get; set; }

    public string LastUpdatedBy { get; set; } = "System";
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public static SystemConfigurationViewModel FromSettings(SystemConfigurationSettings settings)
    {
        return new SystemConfigurationViewModel
        {
            ManageUsersAndRolesEnabled = settings.ManageUsersAndRolesEnabled,
            InvoiceGenerationEnabled = settings.InvoiceGenerationEnabled,
            PaymentProcessingEnabled = settings.PaymentProcessingEnabled,
            CreditDebitManagementEnabled = settings.CreditDebitManagementEnabled,
            RevenueMonitoringEnabled = settings.RevenueMonitoringEnabled,
            AccountIntegrationEnabled = settings.AccountIntegrationEnabled,
            ViewReportsDashboardsEnabled = settings.ViewReportsDashboardsEnabled,
            ViewAuditLogsEnabled = settings.ViewAuditLogsEnabled,
            MaintenanceModeEnabled = settings.MaintenanceModeEnabled,
            SessionTimeoutMinutes = settings.SessionTimeoutMinutes,
            RevenueAlertThreshold = settings.RevenueAlertThreshold,
            MaskedPayMongoSecretKey = MaskSecret(settings.PayMongoSecretKey),
            AccountingApiBaseUrl = settings.AccountingApiBaseUrl,
            OutboxEventsApiUrl = settings.OutboxEventsApiUrl,
            IntegrationAuthMode = string.IsNullOrWhiteSpace(settings.IntegrationAuthMode)
                ? DomainValues.IntegrationAuthMode.None
                : settings.IntegrationAuthMode,
            IntegrationApiKeyHeaderName = string.IsNullOrWhiteSpace(settings.IntegrationApiKeyHeaderName)
                ? "X-Api-Key"
                : settings.IntegrationApiKeyHeaderName,
            MaskedIntegrationApiKey = MaskSecret(settings.IntegrationApiKey),
            MaskedIntegrationBearerToken = MaskSecret(settings.IntegrationBearerToken),
            AutoSyncEnabled = settings.AutoSyncEnabled,
            LastUpdatedBy = settings.LastUpdatedBy,
            LastUpdatedUtc = settings.LastUpdatedUtc
        };
    }

    public void ApplyTo(SystemConfigurationSettings settings, string updatedBy)
    {
        settings.ManageUsersAndRolesEnabled = ManageUsersAndRolesEnabled;
        settings.InvoiceGenerationEnabled = InvoiceGenerationEnabled;
        settings.PaymentProcessingEnabled = PaymentProcessingEnabled;
        settings.CreditDebitManagementEnabled = CreditDebitManagementEnabled;
        settings.RevenueMonitoringEnabled = RevenueMonitoringEnabled;
        settings.AccountIntegrationEnabled = AccountIntegrationEnabled;
        settings.ViewReportsDashboardsEnabled = ViewReportsDashboardsEnabled;
        settings.ViewAuditLogsEnabled = ViewAuditLogsEnabled;
        settings.MaintenanceModeEnabled = MaintenanceModeEnabled;
        settings.SessionTimeoutMinutes = SessionTimeoutMinutes;
        settings.RevenueAlertThreshold = RevenueAlertThreshold;

        if (ClearPayMongoSecretKey)
        {
            settings.PayMongoSecretKey = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(NewPayMongoSecretKey))
        {
            settings.PayMongoSecretKey = NewPayMongoSecretKey.Trim();
        }

        settings.AccountingApiBaseUrl = AccountingApiBaseUrl?.Trim() ?? string.Empty;
        settings.OutboxEventsApiUrl = OutboxEventsApiUrl?.Trim() ?? string.Empty;
        settings.IntegrationAuthMode = IntegrationAuthMode?.Trim() ?? DomainValues.IntegrationAuthMode.None;
        settings.IntegrationApiKeyHeaderName = string.IsNullOrWhiteSpace(IntegrationApiKeyHeaderName)
            ? "X-Api-Key"
            : IntegrationApiKeyHeaderName.Trim();

        if (ClearIntegrationApiKey)
        {
            settings.IntegrationApiKey = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(NewIntegrationApiKey))
        {
            settings.IntegrationApiKey = NewIntegrationApiKey.Trim();
        }

        if (ClearIntegrationBearerToken)
        {
            settings.IntegrationBearerToken = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(NewIntegrationBearerToken))
        {
            settings.IntegrationBearerToken = NewIntegrationBearerToken.Trim();
        }

        settings.AutoSyncEnabled = AutoSyncEnabled;
        settings.LastUpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "System" : updatedBy;
        settings.LastUpdatedUtc = DateTime.UtcNow;
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Not set";
        }

        string trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        string suffix = trimmed[^4..];
        return $"********{suffix}";
    }
}
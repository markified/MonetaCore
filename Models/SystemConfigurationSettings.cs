namespace MonetaCore.Models;

public class SystemConfigurationSettings
{
    public bool ManageUsersAndRolesEnabled { get; set; } = true;
    public bool InvoiceGenerationEnabled { get; set; } = true;
    public bool PaymentProcessingEnabled { get; set; } = true;
    public bool CreditDebitManagementEnabled { get; set; } = true;
    public bool RevenueMonitoringEnabled { get; set; } = true;
    public bool AccountIntegrationEnabled { get; set; } = true;
    public bool ViewReportsDashboardsEnabled { get; set; } = true;
    public bool ViewAuditLogsEnabled { get; set; } = true;

    public bool MaintenanceModeEnabled { get; set; }
    public int SessionTimeoutMinutes { get; set; } = 480;
    public decimal RevenueAlertThreshold { get; set; } = 100000m;

    public string PayMongoSecretKey { get; set; } = string.Empty;
    public string AccountingApiBaseUrl { get; set; } = string.Empty;
    public string OutboxEventsApiUrl { get; set; } = string.Empty;
    public string IntegrationAuthMode { get; set; } = string.Empty;
    public string IntegrationApiKeyHeaderName { get; set; } = "X-Api-Key";
    public string IntegrationApiKey { get; set; } = string.Empty;
    public string IntegrationBearerToken { get; set; } = string.Empty;
    public bool AutoSyncEnabled { get; set; }

    public string LastUpdatedBy { get; set; } = "System";
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
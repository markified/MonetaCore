namespace MonetaCore.Security;

public static class AuthorizationPolicies
{
    public const string SuperAdminOnly = nameof(SuperAdminOnly);
    public const string SuperOrMainAdmin = nameof(SuperOrMainAdmin);
    public const string FinanceOperations = nameof(FinanceOperations);
    public const string ClientManagement = nameof(ClientManagement);
    public const string IntegrationsOperations = nameof(IntegrationsOperations);
    public const string BillingOperations = nameof(BillingOperations);
    public const string FinanceManagerOnly = nameof(FinanceManagerOnly);
    public const string InvoiceCancellation = nameof(InvoiceCancellation);
    public const string AuditAccess = nameof(AuditAccess);
}

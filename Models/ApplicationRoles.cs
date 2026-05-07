namespace MonetaCore.Models;

public static class ApplicationRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string MainAdmin = "MainAdmin";
    public const string FinanceManager = "FinanceManager";
    public const string BillingStaff = "BillingStaff";
    public const string Accountant = "Accountant";
    public const string Auditor = "Auditor";
    public const string Client = "Client";

    public static readonly string[] InternalRoles =
    [
        SuperAdmin,
        MainAdmin,
        FinanceManager,
        BillingStaff,
        Accountant,
        Auditor
    ];

    public static readonly string[] AllRoles =
    [
        SuperAdmin,
        MainAdmin,
        FinanceManager,
        BillingStaff,
        Accountant,
        Auditor,
        Client
    ];
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MonetaCore.Models;
using MonetaCore.Services;

namespace MonetaCore.Filters;

public enum SystemModule
{
    ManageUsersAndRoles,
    InvoiceGeneration,
    PaymentProcessing,
    CreditDebitManagement,
    RevenueMonitoring,
    AccountIntegration,
    ViewReportsDashboards,
    ViewAuditLogs
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireModuleAttribute : Attribute, IAsyncActionFilter
{
    private readonly SystemModule[] _modules;

    public RequireModuleAttribute(params SystemModule[] modules)
    {
        _modules = modules.Length == 0 ? new[] { SystemModule.InvoiceGeneration } : modules;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configurationService = context.HttpContext.RequestServices.GetRequiredService<ISystemConfigurationService>();
        SystemConfigurationSettings settings = await configurationService.GetAsync(context.HttpContext.RequestAborted);

        bool enabled = _modules.All(module => IsEnabled(settings, module));
        if (!enabled)
        {
            context.Result = BuildDisabledResult(context, _modules);
            return;
        }

        await next();
    }

    private static bool IsEnabled(SystemConfigurationSettings settings, SystemModule module)
    {
        return module switch
        {
            SystemModule.ManageUsersAndRoles => settings.ManageUsersAndRolesEnabled,
            SystemModule.InvoiceGeneration => settings.InvoiceGenerationEnabled,
            SystemModule.PaymentProcessing => settings.PaymentProcessingEnabled,
            SystemModule.CreditDebitManagement => settings.CreditDebitManagementEnabled,
            SystemModule.RevenueMonitoring => settings.RevenueMonitoringEnabled,
            SystemModule.AccountIntegration => settings.AccountIntegrationEnabled,
            SystemModule.ViewReportsDashboards => settings.ViewReportsDashboardsEnabled,
            SystemModule.ViewAuditLogs => settings.ViewAuditLogsEnabled,
            _ => true
        };
    }

    private static IActionResult BuildDisabledResult(ActionExecutingContext context, SystemModule[] modules)
    {
        string moduleList = string.Join(", ", modules.Select(module => module.ToString()));
        bool isApiRequest = context.HttpContext.Request.Path.StartsWithSegments("/api") ||
            context.HttpContext.Request.Headers.Accept.Any(value =>
                !string.IsNullOrWhiteSpace(value) && value.Contains("application/json", StringComparison.OrdinalIgnoreCase));

        if (isApiRequest)
        {
            return new ObjectResult(new
            {
                message = "Module disabled by system configuration.",
                modules = moduleList
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return new RedirectToActionResult("ModuleDisabled", "Home", null);
    }
}

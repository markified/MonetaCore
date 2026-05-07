using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.Services;
using MonetaCore.ViewModels;

namespace MonetaCore.Controllers;

[Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
public class SystemConfigurationController : Controller
{
    private readonly ISystemConfigurationService _systemConfigurationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public SystemConfigurationController(
        ISystemConfigurationService systemConfigurationService,
        ICurrentUserService currentUser,
        IAuditService auditService)
    {
        _systemConfigurationService = systemConfigurationService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await _systemConfigurationService.GetAsync(cancellationToken);
        var model = SystemConfigurationViewModel.FromSettings(settings);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SystemConfigurationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var currentSettings = await _systemConfigurationService.GetAsync(cancellationToken);
            var currentModel = SystemConfigurationViewModel.FromSettings(currentSettings);
            model.MaskedPayMongoSecretKey = currentModel.MaskedPayMongoSecretKey;
            model.MaskedIntegrationApiKey = currentModel.MaskedIntegrationApiKey;
            model.MaskedIntegrationBearerToken = currentModel.MaskedIntegrationBearerToken;
            model.NewPayMongoSecretKey = string.Empty;
            model.NewIntegrationApiKey = string.Empty;
            model.NewIntegrationBearerToken = string.Empty;
            return View("Index", model);
        }

        var settings = await _systemConfigurationService.GetAsync(cancellationToken);

        string payMongoAction = model.ClearPayMongoSecretKey
            ? "Cleared"
            : string.IsNullOrWhiteSpace(model.NewPayMongoSecretKey)
                ? "Unchanged"
                : "Rotated";

        string integrationApiKeyAction = model.ClearIntegrationApiKey
            ? "Cleared"
            : string.IsNullOrWhiteSpace(model.NewIntegrationApiKey)
                ? "Unchanged"
                : "Rotated";

        string integrationBearerAction = model.ClearIntegrationBearerToken
            ? "Cleared"
            : string.IsNullOrWhiteSpace(model.NewIntegrationBearerToken)
                ? "Unchanged"
                : "Rotated";

        model.ApplyTo(settings, _currentUser.UserName);
        await _systemConfigurationService.SaveAsync(settings, cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "CONFIG_UPDATE",
            "SystemConfiguration",
            "Global",
            $"MaintenanceMode={settings.MaintenanceModeEnabled}; SessionTimeout={settings.SessionTimeoutMinutes}; RevenueAlertThreshold={settings.RevenueAlertThreshold:N2}; PayMongoSecretKey={payMongoAction}; IntegrationAuthMode={settings.IntegrationAuthMode}; IntegrationApiKey={integrationApiKeyAction}; IntegrationBearerToken={integrationBearerAction}",
            cancellationToken);

        TempData["Success"] = "System configuration settings saved.";
        return RedirectToAction(nameof(Index));
    }
}
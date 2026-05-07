using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.Services;
using MonetaCore.ViewModels;

namespace MonetaCore.Controllers;

[Authorize(Policy = AuthorizationPolicies.IntegrationsOperations)]
[RequireModule(SystemModule.AccountIntegration)]
public class IntegrationsController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISystemConfigurationService _systemConfigurationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public IntegrationsController(
        AppDbContext dbContext,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ISystemConfigurationService systemConfigurationService,
        ICurrentUserService currentUser,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _systemConfigurationService = systemConfigurationService;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? eventType = null,
        string? eventSearch = null,
        int eventPage = 1,
        int eventPageSize = 20,
        string? deadLetterEventType = null,
        string? deadLetterSearch = null,
        int deadLetterPage = 1,
        int deadLetterPageSize = 20,
        CancellationToken cancellationToken = default)
    {
        string eventTypeFilter = eventType?.Trim() ?? string.Empty;
        string eventSearchFilter = eventSearch?.Trim() ?? string.Empty;
        int normalizedEventPageSize = Math.Clamp(eventPageSize, 10, 100);

        IQueryable<AccountIntegrationEvent> eventQuery = _dbContext.IntegrationEvents
            .AsNoTracking()
            .Include(x => x.TriggeredByUser);

        if (!string.IsNullOrWhiteSpace(eventTypeFilter))
        {
            eventQuery = eventQuery.Where(x =>
                x.Provider.Contains(eventTypeFilter) ||
                x.Direction.Contains(eventTypeFilter) ||
                x.Status.Contains(eventTypeFilter));
        }

        if (!string.IsNullOrWhiteSpace(eventSearchFilter))
        {
            eventQuery = eventQuery.Where(x =>
                x.Provider.Contains(eventSearchFilter) ||
                x.Message.Contains(eventSearchFilter) ||
                x.CorrelationId.Contains(eventSearchFilter));
        }

        int totalEvents = await eventQuery.CountAsync(cancellationToken);
        int totalEventPages = totalEvents == 0 ? 1 : (int)Math.Ceiling(totalEvents / (double)normalizedEventPageSize);
        int normalizedEventPage = Math.Clamp(eventPage, 1, totalEventPages);

        var events = await eventQuery
            .OrderByDescending(x => x.SyncedAtUtc)
            .Skip((normalizedEventPage - 1) * normalizedEventPageSize)
            .Take(normalizedEventPageSize)
            .ToListAsync(cancellationToken);

        string deadLetterTypeFilter = deadLetterEventType?.Trim() ?? string.Empty;
        string searchFilter = deadLetterSearch?.Trim() ?? string.Empty;
        int pageSize = Math.Clamp(deadLetterPageSize, 10, 100);

        IQueryable<OutboxMessage> deadLetterQuery = _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Status == DomainValues.OutboxStatus.Failed);

        if (!string.IsNullOrWhiteSpace(deadLetterTypeFilter))
        {
            deadLetterQuery = deadLetterQuery.Where(x => x.EventType.Contains(deadLetterTypeFilter));
        }

        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            bool hasEventIdFilter = Guid.TryParse(searchFilter, out Guid eventIdFilter);

            deadLetterQuery = deadLetterQuery.Where(x =>
                (hasEventIdFilter && x.EventId == eventIdFilter) ||
                x.EventType.Contains(searchFilter) ||
                x.Producer.Contains(searchFilter) ||
                x.CorrelationId.Contains(searchFilter) ||
                x.LastError.Contains(searchFilter));
        }

        int totalDeadLetters = await deadLetterQuery.CountAsync(cancellationToken);
        int totalPages = totalDeadLetters == 0 ? 1 : (int)Math.Ceiling(totalDeadLetters / (double)pageSize);
        int page = Math.Clamp(deadLetterPage, 1, totalPages);

        var deadLetters = await deadLetterQuery
            .OrderByDescending(x => x.LastAttemptedAtUtc ?? x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        string accountingApiUrl = await ResolveAccountingApiUrlAsync(cancellationToken);
        string outboxApiUrl = await ResolveOutboxEventsApiUrlAsync(cancellationToken);
        ConnectorAuthSettings authSettings = await ResolveConnectorAuthSettingsAsync(cancellationToken);

        int pendingOutboxCount = await _dbContext.OutboxMessages
            .AsNoTracking()
            .CountAsync(x => x.Status == DomainValues.OutboxStatus.Pending, cancellationToken);

        int failedOutboxCount = await _dbContext.OutboxMessages
            .AsNoTracking()
            .CountAsync(x => x.Status == DomainValues.OutboxStatus.Failed, cancellationToken);

        int dispatchedOutboxCount = await _dbContext.OutboxMessages
            .AsNoTracking()
            .CountAsync(x => x.Status == DomainValues.OutboxStatus.Dispatched, cancellationToken);

        var model = new IntegrationsIndexViewModel
        {
            AccountingApiEndpoint = string.IsNullOrWhiteSpace(accountingApiUrl) ? "Not configured" : accountingApiUrl,
            OutboxEventsEndpoint = string.IsNullOrWhiteSpace(outboxApiUrl) ? "Not configured" : outboxApiUrl,
            IntegrationAuthMode = string.IsNullOrWhiteSpace(authSettings.Mode)
                ? DomainValues.IntegrationAuthMode.None
                : authSettings.Mode,
            EventTypeFilter = eventTypeFilter,
            EventSearchFilter = eventSearchFilter,
            EventPage = normalizedEventPage,
            EventPageSize = normalizedEventPageSize,
            EventTotalCount = totalEvents,
            EventTotalPages = totalEventPages,
            DeadLetterEventTypeFilter = deadLetterTypeFilter,
            DeadLetterSearchFilter = searchFilter,
            DeadLetterPage = page,
            DeadLetterPageSize = pageSize,
            DeadLetterTotalCount = totalDeadLetters,
            DeadLetterTotalPages = totalPages,
            PendingOutboxCount = pendingOutboxCount,
            FailedOutboxCount = failedOutboxCount,
            DispatchedOutboxCount = dispatchedOutboxCount,
            RecentEvents = events,
            DeadLetters = deadLetters
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncInvoices(
        string? eventType = null,
        string? eventSearch = null,
        int eventPage = 1,
        int eventPageSize = 20,
        string? deadLetterEventType = null,
        string? deadLetterSearch = null,
        int deadLetterPage = 1,
        int deadLetterPageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var invoices = await _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.ClientAccount)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(25)
            .Select(x => new
            {
                x.InvoiceNumber,
                Client = x.ClientAccount != null ? x.ClientAccount.CompanyName : "Unknown",
                x.IssueDateUtc,
                x.DueDateUtc,
                x.TotalAmount,
                x.AmountPaid,
                x.BalanceDue,
                x.Status
            })
            .ToListAsync(cancellationToken);

        string correlationId = CreateCorrelationId("SYNC");

        string payload = JsonSerializer.Serialize(new
        {
            source = "MonetaCore",
            correlationId,
            syncedAtUtc = DateTime.UtcNow,
            records = invoices
        });

        string apiUrl = await ResolveAccountingApiUrlAsync(cancellationToken);
        bool success;
        string message;

        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            success = true;
            message = "Sync simulated. Configure Integrations:AccountingApiBaseUrl for live pushes.";
        }
        else
        {
            try
            {
                HttpClient client = _httpClientFactory.CreateClient();
                ConnectorAuthSettings authSettings = await ResolveConnectorAuthSettingsAsync(cancellationToken);

                using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
                };
                request.Headers.TryAddWithoutValidation("X-MonetaCore-CorrelationId", correlationId);
                ApplyConnectorAuthHeaders(request, authSettings);

                using var response = await client.SendAsync(request, cancellationToken);

                success = response.IsSuccessStatusCode;
                message = success
                    ? $"Sync completed: {(int)response.StatusCode}"
                    : $"Sync failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            }
            catch (Exception ex)
            {
                success = false;
                message = $"Sync failed: {ex.Message}";
            }
        }

        var syncEvent = new AccountIntegrationEvent
        {
            Provider = "ExternalAccounting",
            Direction = "Export",
            Payload = payload,
            Status = success ? DomainValues.SyncStatus.Success : DomainValues.SyncStatus.Failed,
            Message = message,
            CorrelationId = correlationId,
            TriggeredByUserId = _currentUser.UserId,
            SyncedAtUtc = DateTime.UtcNow
        };

        _dbContext.IntegrationEvents.Add(syncEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "SYNC",
            "Integration",
            syncEvent.Id.ToString(),
            $"{message} CorrelationId={correlationId}",
            cancellationToken);

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToFilteredIndex(eventType, eventSearch, eventPage, eventPageSize, deadLetterEventType, deadLetterSearch, deadLetterPage, deadLetterPageSize);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplayDeadLetter(
        long id,
        string? eventType = null,
        string? eventSearch = null,
        int eventPage = 1,
        int eventPageSize = 20,
        string? deadLetterEventType = null,
        string? deadLetterSearch = null,
        int deadLetterPage = 1,
        int deadLetterPageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (message is null)
        {
            TempData["Error"] = "Outbox message not found.";
            return RedirectToFilteredIndex(eventType, eventSearch, eventPage, eventPageSize, deadLetterEventType, deadLetterSearch, deadLetterPage, deadLetterPageSize);
        }

        if (message.Status != DomainValues.OutboxStatus.Failed)
        {
            TempData["Error"] = "Only failed outbox messages can be replayed.";
            return RedirectToFilteredIndex(eventType, eventSearch, eventPage, eventPageSize, deadLetterEventType, deadLetterSearch, deadLetterPage, deadLetterPageSize);
        }

        string replayCorrelationId = CreateCorrelationId("REPLAY-SINGLE");
        ResetForReplay(message, replayCorrelationId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "OUTBOX_REPLAY",
            "OutboxMessage",
            message.Id.ToString(),
            $"Replayed failed outbox event {message.EventType} ({message.EventId}). CorrelationId={replayCorrelationId}",
            cancellationToken);

        TempData["Success"] = $"Dead-letter event queued for replay. Correlation ID: {replayCorrelationId}";
        return RedirectToFilteredIndex(eventType, eventSearch, eventPage, eventPageSize, deadLetterEventType, deadLetterSearch, deadLetterPage, deadLetterPageSize);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplayAllDeadLetters(
        string? eventType = null,
        string? eventSearch = null,
        int eventPage = 1,
        int eventPageSize = 20,
        string? deadLetterEventType = null,
        string? deadLetterSearch = null,
        int deadLetterPage = 1,
        int deadLetterPageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var failedMessages = await _dbContext.OutboxMessages
            .Where(x => x.Status == DomainValues.OutboxStatus.Failed)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        if (failedMessages.Count == 0)
        {
            TempData["Error"] = "No dead-letter messages available for replay.";
            return RedirectToFilteredIndex(eventType, eventSearch, eventPage, eventPageSize, deadLetterEventType, deadLetterSearch, deadLetterPage, deadLetterPageSize);
        }

        string replayCorrelationId = CreateCorrelationId("REPLAY-BATCH");

        foreach (OutboxMessage failed in failedMessages)
        {
            ResetForReplay(failed, replayCorrelationId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "OUTBOX_REPLAY_ALL",
            "OutboxMessage",
            "Batch",
            $"Queued {failedMessages.Count} failed outbox messages for replay. CorrelationId={replayCorrelationId}",
            cancellationToken);

        TempData["Success"] = $"Queued {failedMessages.Count} dead-letter events for replay. Correlation ID: {replayCorrelationId}";
        return RedirectToFilteredIndex(eventType, eventSearch, eventPage, eventPageSize, deadLetterEventType, deadLetterSearch, deadLetterPage, deadLetterPageSize);
    }

    private async Task<string> ResolveAccountingApiUrlAsync(CancellationToken cancellationToken)
    {
        SystemConfigurationSettings settings = await _systemConfigurationService.GetAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(settings.AccountingApiBaseUrl))
        {
            return settings.AccountingApiBaseUrl;
        }

        return _configuration["Integrations:AccountingApiBaseUrl"] ?? string.Empty;
    }

    private async Task<string> ResolveOutboxEventsApiUrlAsync(CancellationToken cancellationToken)
    {
        SystemConfigurationSettings settings = await _systemConfigurationService.GetAsync(cancellationToken);

        string resolved = FirstNonEmpty(
            settings.OutboxEventsApiUrl,
            _configuration["Integrations:OutboxEventsApiUrl"],
            BuildOutboxEndpoint(settings.AccountingApiBaseUrl),
            BuildOutboxEndpoint(_configuration["Integrations:AccountingApiBaseUrl"]));

        return resolved;
    }

    private async Task<ConnectorAuthSettings> ResolveConnectorAuthSettingsAsync(CancellationToken cancellationToken)
    {
        SystemConfigurationSettings settings = await _systemConfigurationService.GetAsync(cancellationToken);

        string mode = FirstNonEmpty(
            settings.IntegrationAuthMode,
            _configuration["Integrations:AuthMode"],
            DomainValues.IntegrationAuthMode.None);

        string apiKeyHeaderName = FirstNonEmpty(
            settings.IntegrationApiKeyHeaderName,
            _configuration["Integrations:ApiKeyHeaderName"],
            "X-Api-Key");

        string apiKey = FirstNonEmpty(
            settings.IntegrationApiKey,
            _configuration["Integrations:ApiKey"]);

        string bearerToken = FirstNonEmpty(
            settings.IntegrationBearerToken,
            _configuration["Integrations:BearerToken"]);

        return new ConnectorAuthSettings(mode, apiKeyHeaderName, apiKey, bearerToken);
    }

    private static void ApplyConnectorAuthHeaders(HttpRequestMessage request, ConnectorAuthSettings settings)
    {
        if (string.Equals(settings.Mode, DomainValues.IntegrationAuthMode.None, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(settings.Mode, DomainValues.IntegrationAuthMode.ApiKey, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new InvalidOperationException("Integration API key authentication is enabled but no API key is configured.");
            }

            string headerName = string.IsNullOrWhiteSpace(settings.ApiKeyHeaderName)
                ? "X-Api-Key"
                : settings.ApiKeyHeaderName;

            request.Headers.Remove(headerName);
            request.Headers.TryAddWithoutValidation(headerName, settings.ApiKey);
            return;
        }

        if (string.Equals(settings.Mode, DomainValues.IntegrationAuthMode.Bearer, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(settings.BearerToken))
            {
                throw new InvalidOperationException("Integration bearer authentication is enabled but no token is configured.");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.BearerToken);
            return;
        }

        throw new InvalidOperationException($"Unsupported integration auth mode '{settings.Mode}'.");
    }

    private static string BuildOutboxEndpoint(string? configuredValue)
    {
        string trimmed = configuredValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsedUri))
        {
            return trimmed;
        }

        if (string.IsNullOrWhiteSpace(parsedUri.AbsolutePath) || parsedUri.AbsolutePath == "/")
        {
            return new Uri(parsedUri, "/api/integration-events").ToString();
        }

        return trimmed;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private IActionResult RedirectToFilteredIndex(
        string? eventType,
        string? eventSearch,
        int eventPage,
        int eventPageSize,
        string? deadLetterEventType,
        string? deadLetterSearch,
        int deadLetterPage,
        int deadLetterPageSize)
    {
        return RedirectToAction(nameof(Index), new
        {
            eventType,
            eventSearch,
            eventPage,
            eventPageSize,
            deadLetterEventType,
            deadLetterSearch,
            deadLetterPage,
            deadLetterPageSize
        });
    }

    private static void ResetForReplay(OutboxMessage message, string replayCorrelationId)
    {
        message.Status = DomainValues.OutboxStatus.Pending;
        message.AttemptCount = 0;
        message.LastError = string.Empty;
        message.ProcessedAtUtc = null;
        message.LastAttemptedAtUtc = null;
        message.NextAttemptAtUtc = DateTime.UtcNow;
        message.CorrelationId = replayCorrelationId;
        message.CausationId = $"REPLAY:{message.Id}";
    }

    private static string CreateCorrelationId(string scope)
    {
        return $"{scope}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
    }

    private sealed record ConnectorAuthSettings(
        string Mode,
        string ApiKeyHeaderName,
        string ApiKey,
        string BearerToken);
}

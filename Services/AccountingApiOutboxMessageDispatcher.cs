using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using MonetaCore.Models;

namespace MonetaCore.Services;

public class AccountingApiOutboxMessageDispatcher : IOutboxMessageDispatcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ISystemConfigurationService _systemConfigurationService;
    private readonly ILogger<AccountingApiOutboxMessageDispatcher> _logger;

    public AccountingApiOutboxMessageDispatcher(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ISystemConfigurationService systemConfigurationService,
        ILogger<AccountingApiOutboxMessageDispatcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _systemConfigurationService = systemConfigurationService;
        _logger = logger;
    }

    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ConnectorDispatchSettings dispatchSettings = await ResolveConnectorDispatchSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(dispatchSettings.Endpoint))
        {
            _logger.LogInformation(
                "Outbox event {EventId} ({EventType}) has no configured connector endpoint. Dispatch skipped.",
                message.EventId,
                message.EventType);
            return;
        }

        var payload = new
        {
            source = "MonetaCore.Outbox",
            dispatchedAtUtc = DateTime.UtcNow,
            eventEnvelope = new
            {
                message.EventId,
                message.EventType,
                message.PayloadVersion,
                message.Producer,
                message.CorrelationId,
                message.CausationId,
                message.IdempotencyKey,
                message.TenantId,
                message.OccurredAtUtc,
                message.PayloadJson
            }
        };

        string requestBody = JsonSerializer.Serialize(payload);
        HttpClient client = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, dispatchSettings.Endpoint);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        request.Headers.Add("X-MonetaCore-EventId", message.EventId.ToString());
        request.Headers.Add("X-MonetaCore-EventType", message.EventType);
        ApplyAuthHeaders(request, dispatchSettings);

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string responsePayload = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Outbox connector returned {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(responsePayload, 240)}");
        }

        _logger.LogInformation(
            "Outbox event {EventId} ({EventType}) dispatched to {Endpoint}.",
            message.EventId,
            message.EventType,
            dispatchSettings.Endpoint);
    }

    private async Task<ConnectorDispatchSettings> ResolveConnectorDispatchSettingsAsync(CancellationToken cancellationToken)
    {
        SystemConfigurationSettings settings = await _systemConfigurationService.GetAsync(cancellationToken);

        string endpoint = FirstNonEmpty(
            settings.OutboxEventsApiUrl,
            _configuration["Integrations:OutboxEventsApiUrl"],
            BuildOutboxEndpoint(settings.AccountingApiBaseUrl),
            BuildOutboxEndpoint(_configuration["Integrations:AccountingApiBaseUrl"]));

        string authMode = FirstNonEmpty(
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

        return new ConnectorDispatchSettings(
            endpoint,
            authMode,
            apiKeyHeaderName,
            apiKey,
            bearerToken);
    }

    private static void ApplyAuthHeaders(HttpRequestMessage request, ConnectorDispatchSettings settings)
    {
        string mode = settings.AuthMode.Trim();
        if (string.Equals(mode, DomainValues.IntegrationAuthMode.None, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(mode, DomainValues.IntegrationAuthMode.ApiKey, StringComparison.OrdinalIgnoreCase))
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

        if (string.Equals(mode, DomainValues.IntegrationAuthMode.Bearer, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(settings.BearerToken))
            {
                throw new InvalidOperationException("Integration bearer authentication is enabled but no token is configured.");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.BearerToken);
            return;
        }

        throw new InvalidOperationException($"Unsupported integration auth mode '{settings.AuthMode}'.");
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

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed record ConnectorDispatchSettings(
        string Endpoint,
        string AuthMode,
        string ApiKeyHeaderName,
        string ApiKey,
        string BearerToken);
}
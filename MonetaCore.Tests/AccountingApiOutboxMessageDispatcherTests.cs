using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MonetaCore.Models;
using MonetaCore.Services;

namespace MonetaCore.Tests;

public class AccountingApiOutboxMessageDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_NoEndpointConfigured_CompletesWithoutCallingHttp()
    {
        int requestCount = 0;
        var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            requestCount += 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var httpClient = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(httpClient);
        var configuration = new ConfigurationBuilder().Build();
        var settings = new FakeSystemConfigurationService
        {
            Settings = new SystemConfigurationSettings
            {
                AccountingApiBaseUrl = string.Empty
            }
        };

        var dispatcher = new AccountingApiOutboxMessageDispatcher(
            factory,
            configuration,
            settings,
            NullLogger<AccountingApiOutboxMessageDispatcher>.Instance);

        var message = CreateMessage();
        await dispatcher.DispatchAsync(message, CancellationToken.None);

        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task DispatchAsync_EndpointReturnsFailure_Throws()
    {
        int requestCount = 0;
        var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            requestCount += 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("connector unavailable")
            });
        });

        var httpClient = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(httpClient);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Integrations:OutboxEventsApiUrl"] = "https://erp.local/integration/events"
            })
            .Build();
        var settings = new FakeSystemConfigurationService();

        var dispatcher = new AccountingApiOutboxMessageDispatcher(
            factory,
            configuration,
            settings,
            NullLogger<AccountingApiOutboxMessageDispatcher>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(CreateMessage(), CancellationToken.None));

        Assert.Contains("Outbox connector returned", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task DispatchAsync_UsesBaseUrlFallbackAndAppendsDefaultPath()
    {
        Uri? capturedUri = null;

        var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var httpClient = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(httpClient);
        var configuration = new ConfigurationBuilder().Build();
        var settings = new FakeSystemConfigurationService
        {
            Settings = new SystemConfigurationSettings
            {
                AccountingApiBaseUrl = "https://erp.local"
            }
        };

        var dispatcher = new AccountingApiOutboxMessageDispatcher(
            factory,
            configuration,
            settings,
            NullLogger<AccountingApiOutboxMessageDispatcher>.Instance);

        await dispatcher.DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.NotNull(capturedUri);
        Assert.Equal("https://erp.local/api/integration-events", capturedUri!.ToString());
    }

    [Fact]
    public async Task DispatchAsync_ApiKeyAuth_AddsConfiguredHeader()
    {
        string? headerValue = null;

        var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            request.Headers.TryGetValues("X-Connector-Key", out IEnumerable<string>? values);
            headerValue = values?.FirstOrDefault();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var dispatcher = new AccountingApiOutboxMessageDispatcher(
            new FakeHttpClientFactory(new HttpClient(handler)),
            new ConfigurationBuilder().Build(),
            new FakeSystemConfigurationService
            {
                Settings = new SystemConfigurationSettings
                {
                    OutboxEventsApiUrl = "https://erp.local/events",
                    IntegrationAuthMode = DomainValues.IntegrationAuthMode.ApiKey,
                    IntegrationApiKeyHeaderName = "X-Connector-Key",
                    IntegrationApiKey = "secret-123"
                }
            },
            NullLogger<AccountingApiOutboxMessageDispatcher>.Instance);

        await dispatcher.DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal("secret-123", headerValue);
    }

    [Fact]
    public async Task DispatchAsync_BearerAuth_AddsAuthorizationHeader()
    {
        string? authorizationHeader = null;

        var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            authorizationHeader = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var dispatcher = new AccountingApiOutboxMessageDispatcher(
            new FakeHttpClientFactory(new HttpClient(handler)),
            new ConfigurationBuilder().Build(),
            new FakeSystemConfigurationService
            {
                Settings = new SystemConfigurationSettings
                {
                    OutboxEventsApiUrl = "https://erp.local/events",
                    IntegrationAuthMode = DomainValues.IntegrationAuthMode.Bearer,
                    IntegrationBearerToken = "token-abc"
                }
            },
            NullLogger<AccountingApiOutboxMessageDispatcher>.Instance);

        await dispatcher.DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal("Bearer token-abc", authorizationHeader);
    }

    private static OutboxMessage CreateMessage()
    {
        return new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = "PortalDisputeSubmitted",
            PayloadVersion = "1.0",
            Producer = "PortalApiController",
            PayloadJson = "{}"
        };
    }
}

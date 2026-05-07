using System.Net;
using System.Net.Http;
using MonetaCore.Models;
using MonetaCore.Services;

namespace MonetaCore.Tests;

internal sealed class FakeCurrentUserService : ICurrentUserService
{
    public int? UserId { get; set; }
    public string UserName { get; set; } = "Test User";
    public string Role { get; set; } = string.Empty;
}

internal sealed class FakeAuditService : IAuditService
{
    public List<(string Action, string EntityName, string EntityId, string Metadata)> Entries { get; } = [];

    public Task LogAsync(
        int? userId,
        string userName,
        string action,
        string entityName,
        string entityId,
        string metadata,
        CancellationToken cancellationToken = default)
    {
        Entries.Add((action, entityName, entityId, metadata));
        return Task.CompletedTask;
    }
}

internal sealed class FakeEventOutboxService : IEventOutboxService
{
    public List<DomainEventEnvelope> Envelopes { get; } = [];

    public Task<Guid> QueueAsync(DomainEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        Envelopes.Add(envelope);
        return Task.FromResult(envelope.EventId);
    }

    public Task<Guid> QueueAsync<TPayload>(
        string eventType,
        string producer,
        TPayload payload,
        string correlationId = "",
        string causationId = "",
        string idempotencyKey = "",
        string tenantId = "",
        CancellationToken cancellationToken = default)
    {
        var envelope = DomainEventEnvelope.Create(
            eventType,
            producer,
            payload,
            correlationId,
            causationId,
            idempotencyKey,
            tenantId);

        Envelopes.Add(envelope);
        return Task.FromResult(envelope.EventId);
    }
}

internal sealed class FakeSystemConfigurationService : ISystemConfigurationService
{
    public SystemConfigurationSettings Settings { get; set; } = new();

    public Task<SystemConfigurationSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Settings);
    }

    public Task SaveAsync(SystemConfigurationSettings settings, CancellationToken cancellationToken = default)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}

internal sealed class FakeInvoiceNumberService : IInvoiceNumberService
{
    public string NextInvoiceNumber { get; set; } = "INV-TEST-0001";

    public Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NextInvoiceNumber);
    }
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public FakeHttpClientFactory(HttpClient client)
    {
        _client = client;
    }

    public HttpClient CreateClient(string name)
    {
        return _client;
    }
}

internal sealed class DelegateHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}

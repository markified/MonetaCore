using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MonetaCore.Controllers;
using MonetaCore.Data;
using MonetaCore.Models;
using MonetaCore.ViewModels;

namespace MonetaCore.Tests;

public class IntegrationsControllerTests
{
    [Fact]
    public async Task Index_AppliesRecentEventFiltersAndPagination()
    {
        await using var dbContext = CreateDbContext();
        DateTime baseline = DateTime.UtcNow;

        for (int i = 0; i < 13; i++)
        {
            dbContext.IntegrationEvents.Add(new AccountIntegrationEvent
            {
                Provider = "ExternalAccounting",
                Direction = "Export",
                Payload = "{}",
                Status = DomainValues.SyncStatus.Success,
                Message = "Invoice sync completed",
                CorrelationId = $"SYNC-{i}",
                SyncedAtUtc = baseline.AddMinutes(-i)
            });
        }

        dbContext.IntegrationEvents.Add(new AccountIntegrationEvent
        {
            Provider = "OutboxConnector",
            Direction = "Export",
            Payload = "{}",
            Status = DomainValues.SyncStatus.Failed,
            Message = "Connector timeout",
            CorrelationId = "REPLAY-1",
            SyncedAtUtc = baseline
        });

        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        IActionResult firstPageResult = await controller.Index(
            eventType: "External",
            eventSearch: "SYNC-",
            eventPage: 1,
            eventPageSize: 10,
            deadLetterEventType: null,
            deadLetterSearch: null,
            deadLetterPage: 1,
            deadLetterPageSize: 10,
            cancellationToken: CancellationToken.None);

        var firstPageView = Assert.IsType<ViewResult>(firstPageResult);
        var firstPageModel = Assert.IsType<IntegrationsIndexViewModel>(firstPageView.Model);

        Assert.Equal(13, firstPageModel.EventTotalCount);
        Assert.Equal(2, firstPageModel.EventTotalPages);
        Assert.Equal(1, firstPageModel.EventPage);
        Assert.Equal(10, firstPageModel.EventPageSize);
        Assert.Equal(10, firstPageModel.RecentEvents.Count);
        Assert.All(firstPageModel.RecentEvents, item => Assert.Equal("ExternalAccounting", item.Provider));
        Assert.All(firstPageModel.RecentEvents, item => Assert.Contains("SYNC-", item.CorrelationId));

        IActionResult secondPageResult = await controller.Index(
            eventType: "External",
            eventSearch: "SYNC-",
            eventPage: 2,
            eventPageSize: 10,
            deadLetterEventType: null,
            deadLetterSearch: null,
            deadLetterPage: 1,
            deadLetterPageSize: 10,
            cancellationToken: CancellationToken.None);

        var secondPageView = Assert.IsType<ViewResult>(secondPageResult);
        var secondPageModel = Assert.IsType<IntegrationsIndexViewModel>(secondPageView.Model);

        Assert.Equal(2, secondPageModel.EventPage);
        Assert.Equal(3, secondPageModel.RecentEvents.Count);
    }

    [Fact]
    public async Task Index_AppliesDeadLetterFiltersAndPagination()
    {
        await using var dbContext = CreateDbContext();
        DateTime baseline = DateTime.UtcNow;

        for (int i = 0; i < 12; i++)
        {
            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "InvoiceCreated",
                Producer = "InvoicesController",
                PayloadVersion = "1.0",
                PayloadJson = "{}",
                Status = DomainValues.OutboxStatus.Failed,
                AttemptCount = 3,
                LastError = "Connector timeout",
                CorrelationId = $"REPLAY-BATCH-{i}",
                CreatedAtUtc = baseline.AddMinutes(-(i + 1)),
                LastAttemptedAtUtc = baseline.AddMinutes(-(i + 1))
            });
        }

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = "PaymentApplied",
            Producer = "PaymentsController",
            PayloadVersion = "1.0",
            PayloadJson = "{}",
            Status = DomainValues.OutboxStatus.Failed,
            AttemptCount = 2,
            LastError = "Validation error",
            CorrelationId = "SYNC-100",
            CreatedAtUtc = baseline,
            LastAttemptedAtUtc = baseline
        });

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = "InvoiceCreated",
            Producer = "InvoicesController",
            PayloadVersion = "1.0",
            PayloadJson = "{}",
            Status = DomainValues.OutboxStatus.Pending,
            AttemptCount = 0,
            LastError = string.Empty,
            CorrelationId = "REPLAY-BATCH-PENDING",
            CreatedAtUtc = baseline,
            LastAttemptedAtUtc = null
        });

        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        IActionResult firstPageResult = await controller.Index(
            deadLetterEventType: "Invoice",
            deadLetterSearch: "REPLAY-BATCH",
            deadLetterPage: 1,
            deadLetterPageSize: 10,
            cancellationToken: CancellationToken.None);

        var firstPageView = Assert.IsType<ViewResult>(firstPageResult);
        var firstPageModel = Assert.IsType<IntegrationsIndexViewModel>(firstPageView.Model);

        Assert.Equal(12, firstPageModel.DeadLetterTotalCount);
        Assert.Equal(2, firstPageModel.DeadLetterTotalPages);
        Assert.Equal(1, firstPageModel.DeadLetterPage);
        Assert.Equal(10, firstPageModel.DeadLetterPageSize);
        Assert.Equal(10, firstPageModel.DeadLetters.Count);
        Assert.All(firstPageModel.DeadLetters, item => Assert.Contains("Invoice", item.EventType));
        Assert.All(firstPageModel.DeadLetters, item => Assert.Contains("REPLAY-BATCH", item.CorrelationId));

        IActionResult secondPageResult = await controller.Index(
            deadLetterEventType: "Invoice",
            deadLetterSearch: "REPLAY-BATCH",
            deadLetterPage: 2,
            deadLetterPageSize: 10,
            cancellationToken: CancellationToken.None);

        var secondPageView = Assert.IsType<ViewResult>(secondPageResult);
        var secondPageModel = Assert.IsType<IntegrationsIndexViewModel>(secondPageView.Model);

        Assert.Equal(2, secondPageModel.DeadLetterPage);
        Assert.Equal(2, secondPageModel.DeadLetters.Count);
    }

    [Fact]
    public async Task Index_NormalizesPageAndPageSizeBounds()
    {
        await using var dbContext = CreateDbContext();

        dbContext.OutboxMessages.AddRange(
            CreateFailedMessage("InvoiceCreated", "REPLAY-1"),
            CreateFailedMessage("InvoiceCreated", "REPLAY-2"),
            CreateFailedMessage("InvoiceCreated", "REPLAY-3"));

        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        IActionResult result = await controller.Index(
            deadLetterEventType: null,
            deadLetterSearch: null,
            deadLetterPage: 99,
            deadLetterPageSize: 5,
            cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<IntegrationsIndexViewModel>(view.Model);

        Assert.Equal(1, model.DeadLetterPage);
        Assert.Equal(10, model.DeadLetterPageSize);
        Assert.Equal(3, model.DeadLetterTotalCount);
        Assert.Equal(1, model.DeadLetterTotalPages);
        Assert.Equal(3, model.DeadLetters.Count);
    }

    private static OutboxMessage CreateFailedMessage(string eventType, string correlationId)
    {
        return new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            Producer = "TestProducer",
            PayloadVersion = "1.0",
            PayloadJson = "{}",
            Status = DomainValues.OutboxStatus.Failed,
            AttemptCount = 2,
            LastError = "Connector timeout",
            CorrelationId = correlationId,
            CreatedAtUtc = DateTime.UtcNow,
            LastAttemptedAtUtc = DateTime.UtcNow
        };
    }

    private static IntegrationsController CreateController(AppDbContext dbContext)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Integrations:AccountingApiBaseUrl"] = "https://accounting.example.local",
                ["Integrations:OutboxEventsApiUrl"] = "https://accounting.example.local/api/integration-events",
                ["Integrations:AuthMode"] = DomainValues.IntegrationAuthMode.None
            })
            .Build();

        var handler = new DelegateHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var httpClientFactory = new FakeHttpClientFactory(new HttpClient(handler));

        return new IntegrationsController(
            dbContext,
            configuration,
            httpClientFactory,
            new FakeSystemConfigurationService { Settings = new SystemConfigurationSettings() },
            new FakeCurrentUserService { UserId = 99, UserName = "ops@local" },
            new FakeAuditService());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.ApiModels;
using MonetaCore.Controllers.Api;
using MonetaCore.Data;
using MonetaCore.Models;

namespace MonetaCore.Tests;

public class IntegrationsOutboxApiControllerTests
{
    [Fact]
    public async Task ReplayDeadLetter_ResetsMessageForRetry()
    {
        await using var dbContext = CreateDbContext();

        var failed = new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = "PortalDisputeSubmitted",
            Producer = "PortalApiController",
            PayloadVersion = "1.0",
            PayloadJson = "{}",
            Status = DomainValues.OutboxStatus.Failed,
            AttemptCount = 5,
            LastError = "Connector timeout",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            LastAttemptedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            NextAttemptAtUtc = null
        };

        dbContext.OutboxMessages.Add(failed);
        await dbContext.SaveChangesAsync();

        var controller = new IntegrationsOutboxApiController(
            dbContext,
            new FakeCurrentUserService { UserId = 4, UserName = "ops@local" },
            new FakeAuditService());

        ActionResult<ApiOutboxReplayResultDto> actionResult = await controller.ReplayDeadLetter(failed.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<ApiOutboxReplayResultDto>(ok.Value);

        OutboxMessage? refreshed = await dbContext.OutboxMessages.SingleOrDefaultAsync(x => x.Id == failed.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(DomainValues.OutboxStatus.Pending, refreshed.Status);
        Assert.Equal(0, refreshed.AttemptCount);
        Assert.True(refreshed.NextAttemptAtUtc.HasValue);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.CorrelationId));
        Assert.StartsWith("REPLAY-SINGLE-", refreshed.CorrelationId);
        Assert.Equal($"REPLAY:{refreshed.Id}", refreshed.CausationId);
        Assert.Equal(DomainValues.OutboxStatus.Pending, payload.Status);
        Assert.Equal(refreshed.CorrelationId, payload.ReplayCorrelationId);
    }

    [Fact]
    public async Task ReplayAllDeadLetters_RequeuesFailedMessagesOnly()
    {
        await using var dbContext = CreateDbContext();

        dbContext.OutboxMessages.AddRange(
            new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "A",
                Producer = "P",
                PayloadVersion = "1.0",
                PayloadJson = "{}",
                Status = DomainValues.OutboxStatus.Failed,
                AttemptCount = 5,
                LastError = "Error"
            },
            new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "B",
                Producer = "P",
                PayloadVersion = "1.0",
                PayloadJson = "{}",
                Status = DomainValues.OutboxStatus.Failed,
                AttemptCount = 3,
                LastError = "Error"
            },
            new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "C",
                Producer = "P",
                PayloadVersion = "1.0",
                PayloadJson = "{}",
                Status = DomainValues.OutboxStatus.Pending,
                AttemptCount = 1
            });

        await dbContext.SaveChangesAsync();

        var controller = new IntegrationsOutboxApiController(
            dbContext,
            new FakeCurrentUserService { UserId = 4, UserName = "ops@local" },
            new FakeAuditService());

        ActionResult<ApiOutboxReplayBatchResultDto> actionResult = await controller.ReplayAllDeadLetters(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<ApiOutboxReplayBatchResultDto>(ok.Value);

        int pendingCount = await dbContext.OutboxMessages.CountAsync(x => x.Status == DomainValues.OutboxStatus.Pending);
        int failedCount = await dbContext.OutboxMessages.CountAsync(x => x.Status == DomainValues.OutboxStatus.Failed);

        Assert.Equal(2, payload.ReplayedCount);
        Assert.False(string.IsNullOrWhiteSpace(payload.ReplayCorrelationId));
        Assert.StartsWith("REPLAY-BATCH-", payload.ReplayCorrelationId);
        Assert.Equal(3, pendingCount);
        Assert.Equal(0, failedCount);

        var replayed = await dbContext.OutboxMessages
            .Where(x => x.EventType == "A" || x.EventType == "B")
            .ToListAsync();

        Assert.All(replayed, message =>
        {
            Assert.Equal(payload.ReplayCorrelationId, message.CorrelationId);
            Assert.Equal($"REPLAY:{message.Id}", message.CausationId);
        });
    }

    [Fact]
    public async Task GetDeadLetters_FiltersAndPaginates()
    {
        await using var dbContext = CreateDbContext();

        dbContext.OutboxMessages.AddRange(
            new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "InvoiceCreated",
                Producer = "InvoicesController",
                PayloadVersion = "1.0",
                PayloadJson = "{}",
                Status = DomainValues.OutboxStatus.Failed,
                AttemptCount = 3,
                LastError = "Timeout",
                CorrelationId = "REPLAY-BATCH-20260101010101-aaaa"
            },
            new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "InvoiceCreated",
                Producer = "InvoicesController",
                PayloadVersion = "1.0",
                PayloadJson = "{}",
                Status = DomainValues.OutboxStatus.Failed,
                AttemptCount = 2,
                LastError = "Validation",
                CorrelationId = "REPLAY-BATCH-20260101010101-bbbb"
            },
            new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "PaymentApplied",
                Producer = "PaymentsController",
                PayloadVersion = "1.0",
                PayloadJson = "{}",
                Status = DomainValues.OutboxStatus.Failed,
                AttemptCount = 1,
                LastError = "Timeout",
                CorrelationId = "SYNC-20260101010101-cccc"
            });

        await dbContext.SaveChangesAsync();

        var controller = new IntegrationsOutboxApiController(
            dbContext,
            new FakeCurrentUserService { UserId = 4, UserName = "ops@local" },
            new FakeAuditService());

        ActionResult<ApiOutboxDeadLetterPageDto> actionResult = await controller.GetDeadLetters(
            eventType: "Invoice",
            search: "REPLAY-BATCH",
            page: 1,
            pageSize: 1,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var payload = Assert.IsType<ApiOutboxDeadLetterPageDto>(ok.Value);

        Assert.Equal(1, payload.Page);
        Assert.Equal(1, payload.PageSize);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.TotalPages);
        Assert.Single(payload.Items);
        Assert.All(payload.Items, item => Assert.Contains("Invoice", item.EventType));
        Assert.All(payload.Items, item => Assert.StartsWith("REPLAY-BATCH", item.CorrelationId));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}

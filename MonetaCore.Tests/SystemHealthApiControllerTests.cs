using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Controllers.Api;
using MonetaCore.Data;
using MonetaCore.Models;

namespace MonetaCore.Tests;

public class SystemHealthApiControllerTests
{
    [Fact]
    public async Task Health_ReturnsDegraded_WhenFailedOutboxMessagesExist()
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
                Status = DomainValues.OutboxStatus.Pending,
                AttemptCount = 1
            },
            new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "InvoiceCreated",
                Producer = "InvoicesController",
                PayloadVersion = "1.0",
                PayloadJson = "{}",
                Status = DomainValues.OutboxStatus.Failed,
                AttemptCount = 5,
                LastError = "Connector timeout"
            });

        await dbContext.SaveChangesAsync();

        var controller = new SystemHealthApiController(dbContext);

        IActionResult actionResult = await controller.Health(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        JsonElement payload = JsonSerializer.SerializeToElement(ok.Value!);

        Assert.Equal("degraded", payload.GetProperty("status").GetString());
        Assert.Equal("connected", payload.GetProperty("database").GetString());
        Assert.Equal(1, payload.GetProperty("outbox").GetProperty("pending").GetInt32());
        Assert.Equal(1, payload.GetProperty("outbox").GetProperty("failed").GetInt32());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}

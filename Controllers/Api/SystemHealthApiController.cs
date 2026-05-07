using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Models;

namespace MonetaCore.Controllers.Api;

[ApiController]
[Route("api/system")]
public class SystemHealthApiController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public SystemHealthApiController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        bool databaseConnected;
        try
        {
            databaseConnected = await _dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            databaseConnected = false;
        }

        int pendingOutboxCount = 0;
        int failedOutboxCount = 0;

        if (databaseConnected)
        {
            pendingOutboxCount = await _dbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(x => x.Status == DomainValues.OutboxStatus.Pending, cancellationToken);

            failedOutboxCount = await _dbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(x => x.Status == DomainValues.OutboxStatus.Failed, cancellationToken);
        }

        string status = !databaseConnected
            ? "unhealthy"
            : failedOutboxCount > 0
                ? "degraded"
                : "healthy";

        return Ok(new
        {
            status,
            checkedAtUtc = DateTime.UtcNow,
            database = databaseConnected ? "connected" : "unavailable",
            outbox = new
            {
                pending = pendingOutboxCount,
                failed = failedOutboxCount
            }
        });
    }
}

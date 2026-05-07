using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.ApiModels;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.Services;

namespace MonetaCore.Controllers.Api;

[ApiController]
[Route("api/integrations/outbox")]
[Authorize(Policy = AuthorizationPolicies.IntegrationsOperations)]
[RequireModule(SystemModule.AccountIntegration)]
public class IntegrationsOutboxApiController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public IntegrationsOutboxApiController(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    [HttpGet("dead-letters")]
    public async Task<ActionResult<ApiOutboxDeadLetterPageDto>> GetDeadLetters(
        [FromQuery] string? eventType,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        string eventTypeFilter = eventType?.Trim() ?? string.Empty;
        string searchFilter = search?.Trim() ?? string.Empty;
        int size = Math.Clamp(pageSize, 1, 100);

        IQueryable<OutboxMessage> deadLetterQuery = _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Status == DomainValues.OutboxStatus.Failed);

        if (!string.IsNullOrWhiteSpace(eventTypeFilter))
        {
            deadLetterQuery = deadLetterQuery.Where(x => x.EventType.Contains(eventTypeFilter));
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

        int totalCount = await deadLetterQuery.CountAsync(cancellationToken);
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)size);
        int normalizedPage = Math.Clamp(page, 1, totalPages);

        var items = await deadLetterQuery
            .OrderByDescending(x => x.LastAttemptedAtUtc ?? x.CreatedAtUtc)
            .Skip((normalizedPage - 1) * size)
            .Take(size)
            .Select(x => new ApiOutboxDeadLetterDto
            {
                Id = x.Id,
                EventId = x.EventId,
                EventType = x.EventType,
                CorrelationId = x.CorrelationId,
                AttemptCount = x.AttemptCount,
                LastError = x.LastError,
                CreatedAtUtc = x.CreatedAtUtc,
                LastAttemptedAtUtc = x.LastAttemptedAtUtc,
                NextAttemptAtUtc = x.NextAttemptAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new ApiOutboxDeadLetterPageDto
        {
            Items = items,
            Page = normalizedPage,
            PageSize = size,
            TotalCount = totalCount,
            TotalPages = totalPages,
            EventTypeFilter = eventTypeFilter,
            SearchFilter = searchFilter
        });
    }

    [HttpPost("{id:long}/replay")]
    public async Task<ActionResult<ApiOutboxReplayResultDto>> ReplayDeadLetter(long id, CancellationToken cancellationToken)
    {
        var message = await _dbContext.OutboxMessages
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (message is null)
        {
            return NotFound(new { message = "Outbox message not found." });
        }

        if (message.Status != DomainValues.OutboxStatus.Failed)
        {
            return BadRequest(new { message = "Only failed outbox messages can be replayed." });
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
            $"Replayed failed outbox event {message.EventType} ({message.EventId}) via API. CorrelationId={replayCorrelationId}",
            cancellationToken);

        return Ok(new ApiOutboxReplayResultDto
        {
            Id = message.Id,
            Status = message.Status,
            AttemptCount = message.AttemptCount,
            NextAttemptAtUtc = message.NextAttemptAtUtc,
            ReplayCorrelationId = replayCorrelationId,
            Message = $"Dead-letter event queued for replay. Correlation ID: {replayCorrelationId}"
        });
    }

    [HttpPost("replay-all")]
    public async Task<ActionResult<ApiOutboxReplayBatchResultDto>> ReplayAllDeadLetters(CancellationToken cancellationToken)
    {
        var failedMessages = await _dbContext.OutboxMessages
            .Where(x => x.Status == DomainValues.OutboxStatus.Failed)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        if (failedMessages.Count == 0)
        {
            return Ok(new ApiOutboxReplayBatchResultDto
            {
                ReplayedCount = 0,
                ReplayCorrelationId = string.Empty,
                Message = "No dead-letter messages available for replay."
            });
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
            $"Queued {failedMessages.Count} failed outbox messages for replay via API. CorrelationId={replayCorrelationId}",
            cancellationToken);

        return Ok(new ApiOutboxReplayBatchResultDto
        {
            ReplayedCount = failedMessages.Count,
            ReplayCorrelationId = replayCorrelationId,
            Message = $"Queued {failedMessages.Count} dead-letter events for replay. Correlation ID: {replayCorrelationId}"
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
}

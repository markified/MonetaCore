namespace MonetaCore.ApiModels;

public class ApiOutboxDeadLetterDto
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastAttemptedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
}

public class ApiOutboxDeadLetterPageDto
{
    public IReadOnlyList<ApiOutboxDeadLetterDto> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public string EventTypeFilter { get; set; } = string.Empty;
    public string SearchFilter { get; set; } = string.Empty;
}

public class ApiOutboxReplayResultDto
{
    public long Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string ReplayCorrelationId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ApiOutboxReplayBatchResultDto
{
    public int ReplayedCount { get; set; }
    public string ReplayCorrelationId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

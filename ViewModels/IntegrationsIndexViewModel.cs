using MonetaCore.Models;

namespace MonetaCore.ViewModels;

public class IntegrationsIndexViewModel
{
    public string AccountingApiEndpoint { get; set; } = "Not configured";
    public string OutboxEventsEndpoint { get; set; } = "Not configured";
    public string IntegrationAuthMode { get; set; } = DomainValues.IntegrationAuthMode.None;

    public string EventTypeFilter { get; set; } = string.Empty;
    public string EventSearchFilter { get; set; } = string.Empty;
    public int EventPage { get; set; } = 1;
    public int EventPageSize { get; set; } = 20;
    public int EventTotalCount { get; set; }
    public int EventTotalPages { get; set; } = 1;

    public string DeadLetterEventTypeFilter { get; set; } = string.Empty;
    public string DeadLetterSearchFilter { get; set; } = string.Empty;
    public int DeadLetterPage { get; set; } = 1;
    public int DeadLetterPageSize { get; set; } = 20;
    public int DeadLetterTotalCount { get; set; }
    public int DeadLetterTotalPages { get; set; } = 1;

    public int PendingOutboxCount { get; set; }
    public int FailedOutboxCount { get; set; }
    public int DispatchedOutboxCount { get; set; }

    public IReadOnlyList<AccountIntegrationEvent> RecentEvents { get; set; } = [];
    public IReadOnlyList<OutboxMessage> DeadLetters { get; set; } = [];
}

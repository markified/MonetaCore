namespace MonetaCore.Services;

public interface IAuditService
{
    Task LogAsync(
        int? userId,
        string userName,
        string action,
        string entityName,
        string entityId,
        string metadata,
        CancellationToken cancellationToken = default);
}

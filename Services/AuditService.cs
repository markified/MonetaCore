using MonetaCore.Data;
using MonetaCore.Models;

namespace MonetaCore.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _dbContext;

    public AuditService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        int? userId,
        string userName,
        string action,
        string entityName,
        string entityId,
        string metadata,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditTrailEntry
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Metadata = metadata,
            TimestampUtc = DateTime.UtcNow
        };

        _dbContext.AuditTrail.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

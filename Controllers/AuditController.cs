using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Security;

namespace MonetaCore.Controllers;

[Authorize(Policy = AuthorizationPolicies.AuditAccess)]
[RequireModule(SystemModule.ViewAuditLogs)]
public class AuditController : Controller
{
    private readonly AppDbContext _dbContext;

    public AuditController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var logs = await _dbContext.AuditTrail
            .AsNoTracking()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(300)
            .ToListAsync(cancellationToken);

        return View(logs);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Filters;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.Services;

namespace MonetaCore.Controllers;

[Authorize(Policy = AuthorizationPolicies.ClientManagement)]
[RequireModule(SystemModule.ManageUsersAndRoles)]
public class ClientsController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public ClientsController(AppDbContext dbContext, ICurrentUserService currentUser, IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var clients = await _dbContext.Clients
            .AsNoTracking()
            .OrderBy(x => x.CompanyName)
            .ToListAsync(cancellationToken);

        return View(clients);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ClientAccount());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClientAccount model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _dbContext.Clients.Add(model);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "CREATE",
            "Client",
            model.Id.ToString(),
            $"Created client {model.CompanyName}",
            cancellationToken);

        TempData["Success"] = "Client account created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var client = await _dbContext.Clients.FindAsync([id], cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        return View(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ClientAccount model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _dbContext.Clients.Update(model);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "UPDATE",
            "Client",
            model.Id.ToString(),
            $"Updated client {model.CompanyName}",
            cancellationToken);

        TempData["Success"] = "Client account updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var client = await _dbContext.Clients
            .AsNoTracking()
            .Include(x => x.Invoices)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (client is null)
        {
            return NotFound();
        }

        ViewBag.InvoiceCount = client.Invoices.Count;
        return View(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var client = await _dbContext.Clients
            .Include(x => x.Invoices)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (client is null)
        {
            return NotFound();
        }

        if (client.Invoices.Count > 0)
        {
            TempData["Error"] = "Cannot delete a client with existing invoices.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        _dbContext.Clients.Remove(client);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            _currentUser.UserId,
            _currentUser.UserName,
            "DELETE",
            "Client",
            client.Id.ToString(),
            $"Deleted client {client.CompanyName}",
            cancellationToken);

        TempData["Success"] = "Client account deleted.";
        return RedirectToAction(nameof(Index));
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.ViewModels;
using MonetaCore.Services;

namespace MonetaCore.Controllers;

public class AccountController : Controller
{
    private const string RegistrationApprovedAction = "REGISTRATION_APPROVED";
    private const string RegistrationRejectedAction = "REGISTRATION_REJECTED";

    private readonly AppDbContext _dbContext;
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _auditService;

    public AccountController(AppDbContext dbContext, IPasswordService passwordService, IAuditService auditService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _auditService = auditService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string returnUrl = "")
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = "")
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail);

        if (user is null || !_passwordService.VerifyPassword(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials. Please try again.");
            return View(model);
        }

        if (!user.IsActive)
        {
            string? rejectionMessage = await GetLatestRegistrationMessageAsync(user.Id, RegistrationRejectedAction);
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(rejectionMessage)
                    ? "Your sign-up request is still pending admin approval."
                    : rejectionMessage);
            return View(model);
        }

        bool hasPreviousSuccessfulLogin = await _dbContext.AuditTrail
            .AsNoTracking()
            .AnyAsync(x => x.UserId == user.Id && x.Action == "LOGIN");

        if (!hasPreviousSuccessfulLogin)
        {
            string? approvalMessage = await GetLatestRegistrationMessageAsync(user.Id, RegistrationApprovedAction);
            if (!string.IsNullOrWhiteSpace(approvalMessage))
            {
                TempData["Success"] = approvalMessage;
            }
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        if (user.ClientAccountId.HasValue)
        {
            claims.Add(new Claim("ClientAccountId", user.ClientAccountId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 24 : 8)
            });

        await _auditService.LogAsync(
            user.Id,
            user.FullName,
            "LOGIN",
            "User",
            user.Id.ToString(),
            $"Login successful. Email: {user.Email}");

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new SignUpViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(SignUpViewModel model)
    {
        model.Role = ApplicationRoles.Client;
        ModelState.Remove(nameof(model.Role));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string normalizedEmail = model.Email.Trim().ToLowerInvariant();
        string fullName = model.FullName.Trim();
        string companyName = model.CompanyName?.Trim() ?? string.Empty;
        string contactPerson = string.IsNullOrWhiteSpace(model.ContactPerson)
            ? fullName
            : model.ContactPerson.Trim();
        string phone = string.IsNullOrWhiteSpace(model.Phone)
            ? string.Empty
            : model.Phone.Trim();
        string address = string.IsNullOrWhiteSpace(model.Address)
            ? string.Empty
            : model.Address.Trim();

        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existingUser is not null)
        {
            string? existingRequestDecision = existingUser.Id > 0
                ? await GetLatestRegistrationMessageAsync(existingUser.Id, RegistrationRejectedAction)
                : null;

            string duplicateMessage = existingUser.IsActive
                ? "An account with this email already exists."
                : string.IsNullOrWhiteSpace(existingRequestDecision)
                    ? "A sign-up request for this email is already pending approval."
                    : "A previous sign-up request for this email was already reviewed. Sign in to view the decision or contact an administrator.";

            ModelState.AddModelError(nameof(model.Email), duplicateMessage);
            return View(model);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(companyName))
            {
                ModelState.AddModelError(nameof(model.CompanyName), "Company name is required for client registration.");
                return View(model);
            }

            var clientAccount = new ClientAccount
            {
                CompanyName = companyName,
                ContactPerson = contactPerson,
                Email = normalizedEmail,
                Phone = phone,
                Address = address,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Clients.Add(clientAccount);
            await _dbContext.SaveChangesAsync();

            var hashedPassword = _passwordService.HashPassword(model.Password);
            var newUser = new AppUser
            {
                FullName = fullName,
                Email = normalizedEmail,
                PasswordHash = hashedPassword,
                Role = ApplicationRoles.Client,
                IsActive = false,
                ClientAccountId = clientAccount.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogAsync(
                null,
                fullName,
                "SIGNUP_REQUEST",
                "User",
                newUser.Id.ToString(),
                $"New {ApplicationRoles.Client} registration created. Email: {normalizedEmail}. Pending admin approval.");

            return RedirectToAction(nameof(SignUpPending), new { email = normalizedEmail });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "An error occurred during registration. Please try again.");
            await _auditService.LogAsync(
                null,
                fullName,
                "SIGNUP_ERROR",
                "User",
                "0",
                $"Registration failed for {normalizedEmail}: {ex.Message}");
            return View(model);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignUpPending(string email)
    {
        ViewData["Email"] = email;
        return View();
    }

    [Authorize(Policy = AuthorizationPolicies.SuperOrMainAdmin)]
    [HttpGet]
    public async Task<IActionResult> PendingRegistrations()
    {
        HashSet<int> rejectedUserIds = await GetRejectedUserIdsAsync();

        var pendingUsers = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.ClientAccount)
            .Where(u => !u.IsActive && !rejectedUserIds.Contains(u.Id))
            .OrderByDescending(u => u.CreatedAtUtc)
            .ToListAsync();

        return View(pendingUsers);
    }

    [Authorize(Policy = AuthorizationPolicies.SuperOrMainAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRegistration(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsActive)
        {
            TempData["Success"] = $"{user.FullName} is already active.";
            return RedirectToAction(nameof(PendingRegistrations));
        }

        user.IsActive = true;
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();

        string adminName = User.Identity?.Name ?? "Admin";
        int? adminId = null;
        string? adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(adminIdClaim, out int parsedAdminId))
        {
            adminId = parsedAdminId;
        }

        await _auditService.LogAsync(
            user.Id,
            user.FullName,
            RegistrationApprovedAction,
            "User",
            user.Id.ToString(),
            "Your MonetaCore registration request has been approved. You can now sign in.");

        await _auditService.LogAsync(
            adminId,
            adminName,
            "APPROVE_REGISTRATION",
            "User",
            user.Id.ToString(),
            $"Approved registration for {user.FullName} ({user.Email}) as {user.Role}.");

        TempData["Success"] = $"Approved registration for {user.FullName}.";

        return RedirectToAction(nameof(PendingRegistrations));
    }

    [Authorize(Policy = AuthorizationPolicies.SuperOrMainAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRegistration(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        string userEmail = user.Email;
        string userName = user.FullName;
        string userRole = user.Role;

        user.IsActive = false;
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();

        string adminName = User.Identity?.Name ?? "Admin";
        int? adminId = null;
        string? adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(adminIdClaim, out int parsedAdminId))
        {
            adminId = parsedAdminId;
        }

        await _auditService.LogAsync(
            user.Id,
            userName,
            RegistrationRejectedAction,
            "User",
            userId.ToString(),
            "Your MonetaCore registration request was rejected. Contact an administrator for assistance.");

        await _auditService.LogAsync(
            adminId,
            adminName,
            "REJECT_REGISTRATION",
            "User",
            userId.ToString(),
            $"Rejected registration for {userName} ({userEmail}) with role {userRole}.");

        TempData["Error"] = $"Rejected registration for {userName}.";

        return RedirectToAction(nameof(PendingRegistrations));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        int? userId = null;
        string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out int parsedId))
        {
            userId = parsedId;
        }

        string userName = User.Identity?.Name ?? "Unknown";
        string email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        await _auditService.LogAsync(
            userId,
            userName,
            "LOGOUT",
            "User",
            userId?.ToString() ?? string.Empty,
            string.IsNullOrWhiteSpace(email) ? "User logged out." : $"Logout successful. Email: {email}");

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task<string?> GetLatestRegistrationMessageAsync(int userId, string action)
    {
        return await _dbContext.AuditTrail
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Action == action)
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => x.Metadata)
            .FirstOrDefaultAsync();
    }

    private async Task<HashSet<int>> GetRejectedUserIdsAsync()
    {
        List<string> rejectedEntityIds = await _dbContext.AuditTrail
            .AsNoTracking()
            .Where(x => x.Action == RegistrationRejectedAction && x.EntityName == "User")
            .Select(x => x.EntityId)
            .ToListAsync();

        return rejectedEntityIds
            .Select(entityId => int.TryParse(entityId, out int parsedId) ? parsedId : (int?)null)
            .Where(parsedId => parsedId.HasValue)
            .Select(parsedId => parsedId!.Value)
            .ToHashSet();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}

using System.Security.Claims;

namespace MonetaCore.Services;

public interface ICurrentUserService
{
    int? UserId { get; }
    string UserName { get; }
    string Role { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            string? value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out int id) ? id : null;
        }
    }

    public string UserName => _httpContextAccessor.HttpContext?.User.Identity?.Name ?? "System";

    public string Role => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}

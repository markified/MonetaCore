using MonetaCore.Models;

namespace MonetaCore.ViewModels;

public class ManageUsersViewModel
{
    public IReadOnlyList<AppUser> Users { get; init; } = [];
    public string? RoleFilter { get; init; }
    public string? Search { get; init; }
    public bool IsSuperAdmin { get; init; }
}

public class EditUserViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>Leave blank to keep existing password.</summary>
    public string? NewPassword { get; set; }
    public string? ConfirmNewPassword { get; set; }
}

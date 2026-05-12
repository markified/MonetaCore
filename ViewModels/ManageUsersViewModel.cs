using System.ComponentModel.DataAnnotations;

namespace MonetaCore.ViewModels;

public class ManageUsersViewModel
{
    public IReadOnlyList<ManageUserRowViewModel> Users { get; init; } = [];
}

public class ManageUserRowViewModel
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class EditUserViewModel
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public IReadOnlyList<string> AvailableRoles { get; set; } = [];
}
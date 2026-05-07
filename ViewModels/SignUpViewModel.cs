using System.ComponentModel.DataAnnotations;

namespace MonetaCore.ViewModels;

public class SignUpViewModel
{
    [Required, Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }
}

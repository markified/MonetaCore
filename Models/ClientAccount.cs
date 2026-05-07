using System.ComponentModel.DataAnnotations;

namespace MonetaCore.Models;

public class ClientAccount
{
    public int Id { get; set; }

    [Required, StringLength(180)]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(140)]
    public string ContactPerson { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(180)]
    public string Email { get; set; } = string.Empty;

    [StringLength(40)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(260)]
    public string Address { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}

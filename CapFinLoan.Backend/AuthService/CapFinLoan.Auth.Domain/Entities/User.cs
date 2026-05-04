using CapFinLoan.Auth.Domain.Constants;
using Microsoft.AspNetCore.Identity;

namespace CapFinLoan.Auth.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = RoleNames.Applicant;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
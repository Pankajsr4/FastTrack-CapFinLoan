using System.ComponentModel.DataAnnotations;

namespace CapFinLoan.Auth.Application.Contracts.Requests;

public class SignupRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[^\w\s]).{8,}$", ErrorMessage = "Password must be at least 8 characters and include uppercase, lowercase, number and special character.")]
    public string Password { get; set; } = string.Empty;
}
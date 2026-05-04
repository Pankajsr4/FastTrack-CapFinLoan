using System.ComponentModel.DataAnnotations;

namespace CapFinLoan.Auth.Application.Contracts.Requests;

public class UpdateUserStatusRequest
{
    [Required]
    public bool IsActive { get; set; }
}
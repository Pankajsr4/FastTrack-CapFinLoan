using System.ComponentModel.DataAnnotations;

namespace CapFinLoan.Admin.Application.Contracts.Requests;

public class ReviewLoanApplicationRequest
{
    [Required]
    public string TargetStatus { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Remarks { get; set; } = string.Empty;
}
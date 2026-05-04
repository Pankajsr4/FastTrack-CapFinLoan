namespace CapFinLoan.Application.Application.Contracts.Requests;

public class WithdrawApplicationRequest
{
    /// <summary>Optional reason the applicant is withdrawing the application.</summary>
    public string? Reason { get; set; }
}

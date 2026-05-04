namespace CapFinLoan.Application.Application.Contracts.Requests;

public class SaveLoanApplicationRequest
{
    public PersonalDetailsRequest PersonalDetails { get; set; } = new();
    public EmploymentDetailsRequest EmploymentDetails { get; set; } = new();
    public LoanDetailsRequest LoanDetails { get; set; } = new();
}
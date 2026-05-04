namespace CapFinLoan.Application.Application.Contracts.Responses;

public class LoanDetailsResponse
{
    public decimal RequestedAmount { get; set; }
    public int RequestedTenureMonths { get; set; }
    public string LoanPurpose { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}
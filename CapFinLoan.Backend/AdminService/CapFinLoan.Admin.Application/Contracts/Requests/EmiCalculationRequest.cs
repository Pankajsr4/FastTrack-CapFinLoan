namespace CapFinLoan.Admin.Application.Contracts.Requests;

public sealed class EmiCalculationRequest
{
    public decimal LoanAmount { get; init; }
    public decimal InterestRate { get; init; }   // annual, e.g. 12.5 means 12.5%
    public int TenureMonths { get; init; }
}

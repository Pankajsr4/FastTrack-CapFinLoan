namespace CapFinLoan.Admin.Application.Contracts.Responses;

public sealed class EmiCalculationResponse
{
    public decimal LoanAmount { get; init; }
    public decimal AnnualInterestRate { get; init; }
    public int TenureMonths { get; init; }
    public decimal MonthlyEmi { get; init; }
    public decimal TotalPayment { get; init; }
    public decimal TotalInterest { get; init; }
}

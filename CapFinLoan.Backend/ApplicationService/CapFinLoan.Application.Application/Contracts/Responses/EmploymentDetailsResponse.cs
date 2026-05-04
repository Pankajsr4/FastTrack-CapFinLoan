namespace CapFinLoan.Application.Application.Contracts.Responses;

public class EmploymentDetailsResponse
{
    public string EmployerName { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public decimal? MonthlyIncome { get; set; }
    public decimal? AnnualIncome { get; set; }
    public decimal ExistingEmiAmount { get; set; }
}
namespace CapFinLoan.Application.Application.Contracts.Requests;

public class EmploymentDetailsRequest
{
    public string EmployerName { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public decimal? MonthlyIncome { get; set; }
    public decimal? AnnualIncome { get; set; }
    public decimal ExistingEmiAmount { get; set; }
}
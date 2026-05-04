namespace CapFinLoan.Admin.Application.Contracts.Responses;

public class AdminApplicationDetailResponse
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid ApplicantUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public decimal? MonthlyIncome { get; set; }
    public decimal? AnnualIncome { get; set; }
    public decimal ExistingEmiAmount { get; set; }
    public decimal RequestedAmount { get; set; }
    public int RequestedTenureMonths { get; set; }
    public string LoanPurpose { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? DisbursedAtUtc { get; set; }
    public decimal? DisbursedAmount { get; set; }
    public IReadOnlyCollection<AdminApplicationStatusHistoryResponse> Timeline { get; set; } = Array.Empty<AdminApplicationStatusHistoryResponse>();
}
using CapFinLoan.Application.Domain.Constants;

namespace CapFinLoan.Application.Domain.Entities;

public class LoanApplication
{
    public Guid Id { get; set; }
    public Guid ApplicantUserId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string Status { get; set; } = ApplicationStatuses.Draft;
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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }

    // ── Edit tracking ─────────────────────────────────────────────────────────
    public bool IsEdited { get; set; } = false;
    public int EditCount { get; set; } = 0;
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }

    // ── Withdrawal ────────────────────────────────────────────────────────────
    public string? WithdrawalReason { get; set; }
    public DateTime? WithdrawnAtUtc { get; set; }

    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
    public ICollection<ApplicationHistory> History { get; set; } = new List<ApplicationHistory>();
}
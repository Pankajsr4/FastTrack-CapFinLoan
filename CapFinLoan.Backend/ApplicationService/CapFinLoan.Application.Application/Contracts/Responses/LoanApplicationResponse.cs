namespace CapFinLoan.Application.Application.Contracts.Responses;

public class LoanApplicationResponse
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid ApplicantUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public PersonalDetailsResponse PersonalDetails { get; set; } = new();
    public EmploymentDetailsResponse EmploymentDetails { get; set; } = new();
    public LoanDetailsResponse LoanDetails { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    // ── Edit tracking (visible to both applicant and admin) ───────────────────
    public bool IsEdited { get; set; }
    public int EditCount { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }

    // ── Withdrawal ────────────────────────────────────────────────────────────
    public string? WithdrawalReason { get; set; }
    public DateTime? WithdrawnAtUtc { get; set; }
}
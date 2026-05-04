namespace CapFinLoan.Admin.Domain.Entities;

public class Decision
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public Guid AdminUserId { get; set; }
    public string DecisionStatus { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public decimal? SanctionAmount { get; set; }
    public decimal? InterestRate { get; set; }
    public DateTime DecisionAtUtc { get; set; } = DateTime.UtcNow;

    public LoanApplication? LoanApplication { get; set; }
}

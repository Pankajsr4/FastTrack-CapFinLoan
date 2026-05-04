namespace CapFinLoan.Application.Domain.Entities;

public class ApplicationStatusHistory
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public LoanApplication? LoanApplication { get; set; }
}
namespace CapFinLoan.Application.Domain.Entities;

/// <summary>
/// Audit trail — captures a full JSON snapshot of the application before and after each edit.
/// </summary>
public class ApplicationHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;   // "Updated" | "Withdrawn" | "Submitted"
    public string? OldData { get; set; }                 // JSON snapshot before change
    public string? NewData { get; set; }                 // JSON snapshot after change

    public LoanApplication LoanApplication { get; set; } = null!;
}

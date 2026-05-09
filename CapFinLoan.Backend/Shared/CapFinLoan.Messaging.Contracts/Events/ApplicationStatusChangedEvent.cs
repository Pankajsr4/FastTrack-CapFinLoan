namespace CapFinLoan.Messaging.Contracts.Events;

public record ApplicationStatusChangedEvent
{
    public Guid ApplicationId { get; init; }
    public Guid ApplicantUserId { get; init; }   // the loan applicant — used for notifications
    public string ApplicationNumber { get; init; } = string.Empty;
    public string PreviousStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public string Remarks { get; init; } = string.Empty;
    public Guid ChangedByUserId { get; init; }
    public DateTime ChangedAtUtc { get; init; }
}

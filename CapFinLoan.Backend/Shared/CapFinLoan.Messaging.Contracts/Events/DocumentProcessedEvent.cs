namespace CapFinLoan.Messaging.Contracts.Events;

/// <summary>
/// Published when a document transitions to UnderReview status (admin picks it up).
/// Consumed by ApplicationService to update application document tracking.
/// </summary>
public record DocumentProcessedEvent
{
    public Guid DocumentId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid UserId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string ProcessedStatus { get; init; } = string.Empty; // "UnderReview", "Verified", "ReuploadRequired"
    public Guid ProcessedByUserId { get; init; }
    public DateTime ProcessedAtUtc { get; init; }
}

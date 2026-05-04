namespace CapFinLoan.Messaging.Contracts.Events;

/// <summary>
/// Published by DocumentService when a document is successfully uploaded.
/// Consumed by ApplicationService (to track pending docs) and AdminService (to queue for review).
/// </summary>
public record DocumentUploadedEvent
{
    public Guid DocumentId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid UserId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTime UploadedAtUtc { get; init; }
}

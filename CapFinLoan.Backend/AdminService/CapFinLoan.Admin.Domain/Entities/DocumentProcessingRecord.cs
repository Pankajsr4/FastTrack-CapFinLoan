namespace CapFinLoan.Admin.Domain.Entities;

/// <summary>
/// Tracks a document through the admin processing pipeline.
/// Created when a DocumentUploadedEvent is received; updated as the document
/// moves through validation → processing → review.
/// </summary>
public class DocumentProcessingRecord
{
    public Guid   Id            { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the document in DocumentService.</summary>
    public Guid   DocumentId    { get; set; }

    /// <summary>Foreign key to the loan application.</summary>
    public Guid   ApplicationId { get; set; }

    /// <summary>Owner of the document.</summary>
    public Guid   UserId        { get; set; }

    public string DocumentType  { get; set; } = string.Empty;
    public string FileName      { get; set; } = string.Empty;
    public string ContentType   { get; set; } = string.Empty;
    public long   FileSizeBytes { get; set; }

    /// <summary>
    /// Current processing status.
    /// Values: Received | Validating | Processing | UnderReview | Failed
    /// </summary>
    public string Status        { get; set; } = DocumentProcessingStatus.Received;

    /// <summary>Populated when Status = Failed.</summary>
    public string? FailureReason { get; set; }

    /// <summary>When the DocumentUploadedEvent was received by this service.</summary>
    public DateTime ReceivedAtUtc   { get; set; } = DateTime.UtcNow;

    /// <summary>When processing completed (status reached UnderReview or Failed).</summary>
    public DateTime? ProcessedAtUtc { get; set; }

    public DateTime UpdatedAtUtc    { get; set; } = DateTime.UtcNow;
}

/// <summary>Status constants for DocumentProcessingRecord.</summary>
public static class DocumentProcessingStatus
{
    public const string Received    = "Received";
    public const string Validating  = "Validating";
    public const string Processing  = "Processing";
    public const string Completed   = "Completed";
    public const string UnderReview = "UnderReview";
    public const string Failed      = "Failed";
}

using CapFinLoan.Document.Domain.Constants;

namespace CapFinLoan.Document.Domain.Entities;

/// <summary>
/// Represents an uploaded document (KYC, income proof, etc.) linked to a loan application.
/// </summary>
public class LoanDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid UserId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Explicit status of this document in the review lifecycle.</summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    // Legacy derived fields — kept for backwards compatibility
    public bool IsVerified => Status == DocumentStatus.Verified;
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? Remarks { get; set; }

    /// <summary>Populated when Status = Failed. Describes why processing failed.</summary>
    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

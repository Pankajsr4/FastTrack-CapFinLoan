namespace CapFinLoan.Document.Application.Contracts.Responses;

public class DocumentResponse
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Explicit status: Pending | UnderReview | Verified | ReuploadRequired | Failed</summary>
    public string Status { get; set; } = "Pending";

    // Kept for backwards compatibility with existing frontend/consumers
    public bool IsVerified { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? Remarks { get; set; }

    /// <summary>Populated when Status = Failed.</summary>
    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

namespace CapFinLoan.Document.Application.Contracts.Requests;

/// <summary>
/// Request body for PATCH /internal/documents/{id}/status.
/// Used by AdminService to transition a document to UnderReview or Failed.
/// </summary>
public sealed record UpdateDocumentStatusRequest
{
    /// <summary>Target status. Accepted values: "UnderReview", "Failed".</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Required when Status = "Failed". Describes why processing failed.</summary>
    public string? FailureReason { get; init; }
}

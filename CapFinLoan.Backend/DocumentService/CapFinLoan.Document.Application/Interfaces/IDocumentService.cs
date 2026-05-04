using CapFinLoan.Document.Application.Contracts.Responses;
using Microsoft.AspNetCore.Http;

namespace CapFinLoan.Document.Application.Interfaces;

public interface IDocumentService
{
    Task<DocumentResponse> UploadAsync(Guid userId, Guid applicationId, string documentType, IFormFile file, CancellationToken cancellationToken = default);
    Task<DocumentResponse> ReplaceAsync(Guid userId, Guid documentId, IFormFile file, string? documentType = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DocumentResponse>> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DocumentResponse>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DocumentResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Stream?> GetFileStreamAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<DocumentResponse> VerifyAsync(Guid documentId, Guid adminUserId, bool isVerified, string? remarks, CancellationToken cancellationToken = default);

    // ── Async processing pipeline transitions ─────────────────────────────────
    // Called by AdminService consumer as the DocumentUploadedEvent moves through
    // the processing pipeline: Pending → Processing → Completed → UnderReview

    /// <summary>
    /// Transitions Pending → Processing.
    /// Called when the consumer picks up the DocumentUploadedEvent and begins work.
    /// Idempotent — if already Processing or beyond, returns current state.
    /// </summary>
    Task<DocumentResponse> MarkProcessingAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions Processing → Completed.
    /// Called when the processing pipeline finishes successfully.
    /// Idempotent — if already Completed, returns current state.
    /// </summary>
    Task<DocumentResponse> MarkCompletedAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions Completed → UnderReview.
    /// Called when the admin review queue picks up the document.
    /// Idempotent — if already UnderReview or beyond, returns current state.
    /// </summary>
    Task<DocumentResponse> MarkUnderReviewAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions any state → Failed with an optional failure reason.
    /// Called by AdminService when document processing fails permanently.
    /// Idempotent — if already Failed, returns current state without error.
    /// </summary>
    Task<DocumentResponse> MarkFailedAsync(Guid documentId, string? failureReason, CancellationToken cancellationToken = default);
}

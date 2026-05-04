namespace CapFinLoan.Document.Domain.Constants;

/// <summary>
/// Tracks a document through two overlapping lifecycles:
///
/// ── Async processing pipeline (triggered by DocumentUploadedEvent) ───────────
///   Pending     → document uploaded, event published, awaiting consumer pickup
///   Processing  → consumer received the event, pipeline is actively running
///   Completed   → pipeline finished successfully (document queued for admin review)
///   Failed      → pipeline failed permanently (see FailureReason on LoanDocument)
///
/// ── Admin review lifecycle (triggered by admin actions) ──────────────────────
///   Pending       → awaiting admin review (same starting state as above)
///   UnderReview   → admin has opened the document for review
///   Verified      → admin approved the document
///   ReuploadRequired → admin rejected, applicant must re-upload
///
/// The two lifecycles share Pending as the initial state.
/// Processing/Completed/Failed are set by the background consumer.
/// UnderReview/Verified/ReuploadRequired are set by admin actions.
/// </summary>
public enum DocumentStatus
{
    // ── Shared initial state ──────────────────────────────────────────────────

    /// <summary>Uploaded, awaiting processing or admin review.</summary>
    Pending,

    // ── Async processing pipeline ─────────────────────────────────────────────

    /// <summary>Consumer received the event — pipeline is actively running.</summary>
    Processing,

    /// <summary>Pipeline completed — document successfully queued for admin review.</summary>
    Completed,

    /// <summary>Processing failed permanently — see LoanDocument.FailureReason.</summary>
    Failed,

    // ── Admin review lifecycle ────────────────────────────────────────────────

    /// <summary>Admin is actively reviewing the document.</summary>
    UnderReview,

    /// <summary>Admin has approved the document.</summary>
    Verified,

    /// <summary>Admin has rejected and requested a fresh upload.</summary>
    ReuploadRequired
}

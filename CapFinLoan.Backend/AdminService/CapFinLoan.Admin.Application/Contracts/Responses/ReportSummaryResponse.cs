namespace CapFinLoan.Admin.Application.Contracts.Responses;

public sealed class ReportSummaryResponse
{
    public int TotalApplications { get; init; }
    public int DraftCount { get; init; }
    public int SubmittedCount { get; init; }
    public int DocsPendingCount { get; init; }
    public int DocsVerifiedCount { get; init; }
    public int UnderReviewCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
    public int ClosedCount { get; init; }
    public int PendingCount { get; init; }   // Submitted + DocsPending + DocsVerified + UnderReview
    public decimal TotalRequestedAmount { get; init; }
    public decimal TotalApprovedAmount { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
}

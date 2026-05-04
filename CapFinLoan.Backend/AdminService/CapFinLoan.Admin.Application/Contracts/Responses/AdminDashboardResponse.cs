namespace CapFinLoan.Admin.Application.Contracts.Responses;

public class AdminDashboardResponse
{
    // ── Totals ────────────────────────────────────────────────────────────────
    public int TotalApplications { get; set; }
    public int SubmittedCount    { get; set; }
    public int DocsPendingCount  { get; set; }
    public int DocsVerifiedCount { get; set; }
    public int UnderReviewCount  { get; set; }
    public int ApprovedCount     { get; set; }
    public int RejectedCount     { get; set; }
    public int DisbursedCount    { get; set; }

    /// <summary>Submitted + DocsPending + DocsVerified + UnderReview</summary>
    public int PendingCount      { get; set; }

    // ── Financials ────────────────────────────────────────────────────────────
    public decimal TotalRequestedAmount { get; set; }
    public decimal TotalApprovedAmount  { get; set; }
    public decimal TotalDisbursedAmount { get; set; }

    // ── Monthly breakdown (last 12 months) ───────────────────────────────────
    public IReadOnlyList<MonthlyStatEntry> MonthlyStats { get; set; } = [];

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MonthlyStatEntry
{
    /// <summary>e.g. "2026-04"</summary>
    public string Month        { get; set; } = string.Empty;
    public int    Submitted    { get; set; }
    public int    Approved     { get; set; }
    public int    Rejected     { get; set; }
    public int    Disbursed    { get; set; }
    public decimal TotalAmount { get; set; }
}

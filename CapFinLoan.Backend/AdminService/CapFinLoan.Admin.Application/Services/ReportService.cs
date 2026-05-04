using System.Text;
using CapFinLoan.Admin.Application.Contracts.Responses;
using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Constants;
using CapFinLoan.Admin.Domain.Entities;

namespace CapFinLoan.Admin.Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IAdminLoanApplicationRepository _repository;

    public ReportService(IAdminLoanApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ReportSummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        var apps = await _repository.GetAllAsync(ct);

        var pending = Count(apps, ApplicationStatuses.Submitted)
                    + Count(apps, ApplicationStatuses.DocsPending)
                    + Count(apps, ApplicationStatuses.DocsVerified)
                    + Count(apps, ApplicationStatuses.UnderReview);

        var approvedApps = apps
            .Where(a => string.Equals(a.Status, ApplicationStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ReportSummaryResponse
        {
            TotalApplications  = apps.Count,
            DraftCount         = Count(apps, ApplicationStatuses.Draft),
            SubmittedCount     = Count(apps, ApplicationStatuses.Submitted),
            DocsPendingCount   = Count(apps, ApplicationStatuses.DocsPending),
            DocsVerifiedCount  = Count(apps, ApplicationStatuses.DocsVerified),
            UnderReviewCount   = Count(apps, ApplicationStatuses.UnderReview),
            ApprovedCount      = Count(apps, ApplicationStatuses.Approved),
            RejectedCount      = Count(apps, ApplicationStatuses.Rejected),
            ClosedCount        = Count(apps, ApplicationStatuses.Closed),
            PendingCount       = pending,
            TotalRequestedAmount = apps.Sum(a => a.RequestedAmount),
            TotalApprovedAmount  = approvedApps.Sum(a => a.RequestedAmount),
            GeneratedAtUtc     = DateTime.UtcNow
        };
    }

    public async Task<byte[]> ExportCsvAsync(CancellationToken ct = default)
    {
        var apps = await _repository.GetAllAsync(ct);

        var csv = new StringBuilder();

        // Header row
        csv.AppendLine(
            "ApplicationNumber,ApplicantName,Email,Phone," +
            "Status,RequestedAmount,TenureMonths,LoanPurpose," +
            "EmployerName,EmploymentType,MonthlyIncome,AnnualIncome," +
            "City,State,SubmittedAt,CreatedAt,UpdatedAt");

        foreach (var a in apps.OrderByDescending(x => x.CreatedAtUtc))
        {
            csv.AppendLine(string.Join(",",
                Escape(a.ApplicationNumber),
                Escape($"{a.FirstName} {a.LastName}".Trim()),
                Escape(a.Email),
                Escape(a.Phone),
                Escape(a.Status),
                a.RequestedAmount.ToString("F2"),
                a.RequestedTenureMonths.ToString(),
                Escape(a.LoanPurpose),
                Escape(a.EmployerName),
                Escape(a.EmploymentType),
                a.MonthlyIncome?.ToString("F2") ?? "",
                a.AnnualIncome?.ToString("F2") ?? "",
                Escape(a.City),
                Escape(a.State),
                a.SubmittedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                a.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                a.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")
            ));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int Count(IReadOnlyCollection<LoanApplication> apps, string status) =>
        apps.Count(a => string.Equals(a.Status, status, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// RFC 4180 CSV escaping: wrap in quotes if the value contains a comma,
    /// double-quote, or newline; escape internal double-quotes by doubling them.
    /// </summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

using CapFinLoan.Admin.Application.Contracts.Requests;
using CapFinLoan.Admin.Application.Contracts.Responses;
using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Constants;
using CapFinLoan.Admin.Domain.Entities;
using CapFinLoan.Api.Shared.Caching;
using CapFinLoan.Messaging.Contracts.Events;

namespace CapFinLoan.Admin.Application.Services;

public class AdminLoanApplicationService : IAdminLoanApplicationService
{
    private static readonly HashSet<string> AllowedDecisionStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ApplicationStatuses.DocsPending,
        ApplicationStatuses.UnderReview,
        ApplicationStatuses.Approved,
        ApplicationStatuses.Rejected,
        ApplicationStatuses.Disbursed
    };

    private readonly IAdminLoanApplicationRepository _adminLoanApplicationRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICacheService _cache;

    public AdminLoanApplicationService(
        IAdminLoanApplicationRepository adminLoanApplicationRepository,
        IEventPublisher eventPublisher,
        ICacheService cache)
    {
        _adminLoanApplicationRepository = adminLoanApplicationRepository;
        _eventPublisher = eventPublisher;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<AdminApplicationSummaryResponse>> GetQueueAsync(string? status, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"admin:queue:{status ?? "all"}";
        var cached = await _cache.GetAsync<AdminApplicationSummaryResponse[]>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var applications = await _adminLoanApplicationRepository.GetQueueAsync(status, cancellationToken);
        var result = applications.Select(MapSummary).ToArray();

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return result;
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "admin:dashboard";
        var cached = await _cache.GetAsync<AdminDashboardResponse>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        // ── Optimized: two DB round-trips with GROUP BY instead of loading all rows ──
        var stats = await _adminLoanApplicationRepository.GetDashboardStatsAsync(cancellationToken);

        // Build a fast lookup: status → count
        var byStatus = stats.StatusCounts.ToDictionary(
            x => x.Status, x => x.Count, StringComparer.OrdinalIgnoreCase);

        int Get(string s) => byStatus.TryGetValue(s, out var v) ? v : 0;

        var submitted    = Get(ApplicationStatuses.Submitted);
        var docsPending  = Get(ApplicationStatuses.DocsPending);
        var docsVerified = Get(ApplicationStatuses.DocsVerified);
        var underReview  = Get(ApplicationStatuses.UnderReview);
        var approved     = Get(ApplicationStatuses.Approved);
        var rejected     = Get(ApplicationStatuses.Rejected);
        var disbursed    = Get(ApplicationStatuses.Disbursed);

        // ── Build monthly stats (last 12 months, fill gaps with zeros) ────────
        var monthlyStats = BuildMonthlyStats(stats.MonthlyRaw);

        var result = new AdminDashboardResponse
        {
            TotalApplications    = stats.StatusCounts.Sum(x => x.Count),
            SubmittedCount       = submitted,
            DocsPendingCount     = docsPending,
            DocsVerifiedCount    = docsVerified,
            UnderReviewCount     = underReview,
            ApprovedCount        = approved,
            RejectedCount        = rejected,
            DisbursedCount       = disbursed,
            PendingCount         = submitted + docsPending + docsVerified + underReview,
            TotalRequestedAmount = stats.TotalRequested,
            TotalApprovedAmount  = stats.TotalApproved,
            TotalDisbursedAmount = stats.TotalDisbursed,
            MonthlyStats         = monthlyStats,
            GeneratedAtUtc       = DateTime.UtcNow
        };

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return result;
    }

    /// <summary>
    /// Pivots the raw monthly rows into a 12-month series, filling missing months with zeros.
    /// </summary>
    private static IReadOnlyList<MonthlyStatEntry> BuildMonthlyStats(
        IReadOnlyList<MonthlyRawEntry> raw)
    {
        // Index raw data: (year, month, status) → count/amount
        var lookup = raw.ToDictionary(
            r => (r.Year, r.Month, r.Status),
            r => (r.Count, r.Amount));

        var now    = DateTime.UtcNow;
        var result = new List<MonthlyStatEntry>(12);

        for (int i = 11; i >= 0; i--)
        {
            var date  = now.AddMonths(-i);
            var year  = date.Year;
            var month = date.Month;
            var label = $"{year}-{month:D2}";

            int    GetCount(string s) => lookup.TryGetValue((year, month, s), out var v) ? v.Count  : 0;
            decimal GetAmt(string s)  => lookup.TryGetValue((year, month, s), out var v) ? v.Amount : 0m;

            result.Add(new MonthlyStatEntry
            {
                Month     = label,
                Submitted = GetCount(ApplicationStatuses.Submitted)
                          + GetCount(ApplicationStatuses.DocsPending)
                          + GetCount(ApplicationStatuses.DocsVerified)
                          + GetCount(ApplicationStatuses.UnderReview)
                          + GetCount(ApplicationStatuses.Approved)
                          + GetCount(ApplicationStatuses.Rejected)
                          + GetCount(ApplicationStatuses.Disbursed),
                Approved  = GetCount(ApplicationStatuses.Approved),
                Rejected  = GetCount(ApplicationStatuses.Rejected),
                Disbursed = GetCount(ApplicationStatuses.Disbursed),
                TotalAmount = GetAmt(ApplicationStatuses.Submitted)
                            + GetAmt(ApplicationStatuses.DocsPending)
                            + GetAmt(ApplicationStatuses.DocsVerified)
                            + GetAmt(ApplicationStatuses.UnderReview)
                            + GetAmt(ApplicationStatuses.Approved)
                            + GetAmt(ApplicationStatuses.Rejected)
                            + GetAmt(ApplicationStatuses.Disbursed)
            });
        }

        return result;
    }

    public async Task<AdminApplicationDetailResponse> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await _adminLoanApplicationRepository.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Application not found.");

        return MapDetail(application);
    }

    public async Task<AdminApplicationDetailResponse> DisburseAsync(
        Guid applicationId,
        Guid adminUserId,
        decimal disbursedAmount,
        CancellationToken cancellationToken = default)
    {
        var application = await _adminLoanApplicationRepository.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Application not found.");

        if (!string.Equals(application.Status, ApplicationStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Only Approved applications can be disbursed. Current status: '{application.Status}'.");

        if (disbursedAmount <= 0)
            throw new ArgumentException("Disbursed amount must be greater than zero.", nameof(disbursedAmount));

        var now = DateTime.UtcNow;
        var previousStatus = application.Status;

        application.Status          = ApplicationStatuses.Disbursed;
        application.DisbursedAtUtc  = now;
        application.DisbursedAmount = disbursedAmount;
        application.UpdatedAtUtc    = now;

        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            LoanApplicationId = application.Id,
            FromStatus        = previousStatus,
            ToStatus          = ApplicationStatuses.Disbursed,
            Remarks           = $"Loan disbursed. Amount: {disbursedAmount:F2}",
            ChangedByUserId   = adminUserId,
            ChangedAtUtc      = now
        });

        application.Decisions.Add(new Decision
        {
            LoanApplicationId = application.Id,
            AdminUserId       = adminUserId,
            DecisionStatus    = ApplicationStatuses.Disbursed,
            Remarks           = $"Loan disbursed. Amount: {disbursedAmount:F2}",
            SanctionAmount    = disbursedAmount,
            InterestRate      = null,
            DecisionAtUtc     = now
        });

        await _adminLoanApplicationRepository.UpdateAsync(application, cancellationToken);

        await _cache.RemoveByPrefixAsync("admin:queue:", cancellationToken);
        await _cache.RemoveAsync("admin:dashboard", cancellationToken);

        await _eventPublisher.PublishAsync(new ApplicationStatusChangedEvent
        {
            ApplicationId    = application.Id,
            ApplicationNumber = application.ApplicationNumber,
            PreviousStatus   = previousStatus,
            NewStatus        = ApplicationStatuses.Disbursed,
            Remarks          = $"Loan disbursed. Amount: {disbursedAmount:F2}",
            ChangedByUserId  = adminUserId,
            ChangedAtUtc     = now
        }, cancellationToken);

        return MapDetail(application);
    }

    public async Task<AdminApplicationDetailResponse> UpdateStatusAsync(Guid applicationId, Guid reviewerUserId, ReviewLoanApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var application = await _adminLoanApplicationRepository.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Application not found.");

        var targetStatus = NormalizeStatus(request.TargetStatus);
        ValidateTransition(application.Status, targetStatus, request.Remarks);

        var now = DateTime.UtcNow;
        var previousStatus = application.Status;

        application.Status = targetStatus;
        application.UpdatedAtUtc = now;
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            LoanApplicationId = application.Id,
            FromStatus = previousStatus,
            ToStatus = targetStatus,
            Remarks = request.Remarks.Trim(),
            ChangedByUserId = reviewerUserId,
            ChangedAtUtc = now
        });

        application.Decisions.Add(new Decision
        {
            LoanApplicationId = application.Id,
            AdminUserId = reviewerUserId,
            DecisionStatus = targetStatus,
            Remarks = request.Remarks.Trim(),
            SanctionAmount = string.Equals(targetStatus, ApplicationStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                ? application.RequestedAmount
                : null,
            InterestRate = null,
            DecisionAtUtc = now
        });

        await _adminLoanApplicationRepository.UpdateAsync(application, cancellationToken);

        // Invalidate queue and dashboard caches
        await _cache.RemoveByPrefixAsync("admin:queue:", cancellationToken);
        await _cache.RemoveAsync("admin:dashboard", cancellationToken);

        await _eventPublisher.PublishAsync(new ApplicationStatusChangedEvent
        {
            ApplicationId = application.Id,
            ApplicationNumber = application.ApplicationNumber,
            PreviousStatus = previousStatus,
            NewStatus = targetStatus,
            Remarks = request.Remarks.Trim(),
            ChangedByUserId = reviewerUserId,
            ChangedAtUtc = now
        }, cancellationToken);

        return MapDetail(application);
    }

    private static void ValidateTransition(string currentStatus, string targetStatus, string remarks)
    {
        if (!AllowedDecisionStatuses.Contains(targetStatus))
            throw new InvalidOperationException("Target status is not supported by the admin workflow.");

        if (string.Equals(currentStatus, ApplicationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Draft applications cannot be reviewed by admin.");

        if (string.Equals(currentStatus, targetStatus, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Application is already in the requested status.");

        if (string.Equals(targetStatus, ApplicationStatuses.Rejected, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(remarks))
            throw new InvalidOperationException("Remarks are required when rejecting an application.");

        if (string.Equals(targetStatus, ApplicationStatuses.DocsPending, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(remarks))
            throw new InvalidOperationException("Remarks are required when requesting document re-upload.");

        var allowedFromStatuses = targetStatus switch
        {
            ApplicationStatuses.DocsPending => new[] { ApplicationStatuses.Submitted, ApplicationStatuses.DocsVerified, ApplicationStatuses.UnderReview },
            ApplicationStatuses.UnderReview => new[] { ApplicationStatuses.Submitted, ApplicationStatuses.DocsPending, ApplicationStatuses.DocsVerified },
            ApplicationStatuses.Approved    => new[] { ApplicationStatuses.Submitted, ApplicationStatuses.DocsPending, ApplicationStatuses.DocsVerified, ApplicationStatuses.UnderReview },
            ApplicationStatuses.Rejected    => new[] { ApplicationStatuses.Submitted, ApplicationStatuses.DocsPending, ApplicationStatuses.DocsVerified, ApplicationStatuses.UnderReview },
            ApplicationStatuses.Disbursed   => new[] { ApplicationStatuses.Approved },
            _ => Array.Empty<string>()
        };

        if (!allowedFromStatuses.Contains(currentStatus, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Status cannot be changed from {currentStatus} to {targetStatus}.");
    }

    private static string NormalizeStatus(string status)
    {
        var compact = status.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
        return compact switch
        {
            var v when v.Equals("DocsPending",      StringComparison.OrdinalIgnoreCase) => ApplicationStatuses.DocsPending,
            var v when v.Equals("PendingDocuments", StringComparison.OrdinalIgnoreCase) => ApplicationStatuses.DocsPending,
            var v when v.Equals("DocsVerified",     StringComparison.OrdinalIgnoreCase) => ApplicationStatuses.DocsVerified,
            var v when v.Equals("UnderReview",      StringComparison.OrdinalIgnoreCase) => ApplicationStatuses.UnderReview,
            var v when v.Equals("Approved",         StringComparison.OrdinalIgnoreCase) => ApplicationStatuses.Approved,
            var v when v.Equals("Rejected",         StringComparison.OrdinalIgnoreCase) => ApplicationStatuses.Rejected,
            var v when v.Equals("Disbursed",        StringComparison.OrdinalIgnoreCase) => ApplicationStatuses.Disbursed,
            _ => status.Trim()
        };
    }

    private static int CountByStatus(IEnumerable<LoanApplication> applications, string status) =>
        applications.Count(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));

    private static AdminApplicationSummaryResponse MapSummary(LoanApplication application) =>
        new()
        {
            Id = application.Id,
            ApplicationNumber = application.ApplicationNumber,
            ApplicantUserId = application.ApplicantUserId,
            ApplicantName = $"{application.FirstName} {application.LastName}".Trim(),
            Email = application.Email,
            Phone = application.Phone,
            RequestedAmount = application.RequestedAmount,
            RequestedTenureMonths = application.RequestedTenureMonths,
            Status = application.Status,
            CreatedAtUtc = application.CreatedAtUtc,
            UpdatedAtUtc = application.UpdatedAtUtc,
            SubmittedAtUtc = application.SubmittedAtUtc
        };

    private static AdminApplicationDetailResponse MapDetail(LoanApplication application) =>
        new()
        {
            Id = application.Id,
            ApplicationNumber = application.ApplicationNumber,
            ApplicantUserId = application.ApplicantUserId,
            Status = application.Status,
            FirstName = application.FirstName,
            LastName = application.LastName,
            DateOfBirth = application.DateOfBirth,
            Gender = application.Gender,
            Email = application.Email,
            Phone = application.Phone,
            AddressLine1 = application.AddressLine1,
            AddressLine2 = application.AddressLine2,
            City = application.City,
            State = application.State,
            PostalCode = application.PostalCode,
            EmployerName = application.EmployerName,
            EmploymentType = application.EmploymentType,
            MonthlyIncome = application.MonthlyIncome,
            AnnualIncome = application.AnnualIncome,
            ExistingEmiAmount = application.ExistingEmiAmount,
            RequestedAmount = application.RequestedAmount,
            RequestedTenureMonths = application.RequestedTenureMonths,
            LoanPurpose = application.LoanPurpose,
            Remarks = application.Remarks,
            CreatedAtUtc = application.CreatedAtUtc,
            UpdatedAtUtc = application.UpdatedAtUtc,
            SubmittedAtUtc = application.SubmittedAtUtc,
            DisbursedAtUtc = application.DisbursedAtUtc,
            DisbursedAmount = application.DisbursedAmount,
            Timeline = application.StatusHistory
                .OrderByDescending(x => x.ChangedAtUtc)
                .Select(x => new AdminApplicationStatusHistoryResponse
                {
                    Status = x.ToStatus,
                    FromStatus = x.FromStatus,
                    ToStatus = x.ToStatus,
                    Remarks = x.Remarks,
                    ChangedByUserId = x.ChangedByUserId,
                    CreatedAtUtc = x.ChangedAtUtc,
                    ChangedAtUtc = x.ChangedAtUtc
                })
                .ToArray()
        };
}

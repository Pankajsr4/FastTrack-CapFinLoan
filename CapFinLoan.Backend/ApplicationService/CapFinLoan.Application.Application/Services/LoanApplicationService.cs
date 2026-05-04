using System.Text.Json;
using CapFinLoan.Api.Shared.Caching;
using CapFinLoan.Application.Application.Contracts.Requests;
using CapFinLoan.Application.Application.Contracts.Responses;
using CapFinLoan.Application.Application.Interfaces;
using CapFinLoan.Application.Domain.Constants;
using CapFinLoan.Application.Domain.Entities;
using CapFinLoan.Messaging.Contracts.Events;

namespace CapFinLoan.Application.Application.Services;

public class LoanApplicationService : ILoanApplicationService
{
    private readonly ILoanApplicationRepository _loanApplicationRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICacheService _cache;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public LoanApplicationService(
        ILoanApplicationRepository loanApplicationRepository,
        IEventPublisher eventPublisher,
        ICacheService cache)
    {
        _loanApplicationRepository = loanApplicationRepository;
        _eventPublisher = eventPublisher;
        _cache = cache;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<LoanApplicationResponse> CreateDraftAsync(
        Guid applicantUserId,
        SaveLoanApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var application = new LoanApplication
        {
            ApplicantUserId = applicantUserId,
            ApplicationNumber = $"APP-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        ApplyRequest(application, request, now);
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            LoanApplicationId = application.Id,
            FromStatus = string.Empty,
            ToStatus = ApplicationStatuses.Draft,
            Remarks = "Application draft created.",
            ChangedByUserId = applicantUserId,
            ChangedAtUtc = now
        });

        await _loanApplicationRepository.AddAsync(application, cancellationToken);
        return Map(application);
    }

    // ── Update (Draft only — legacy path kept for backward compat) ────────────

    public async Task<LoanApplicationResponse> UpdateDraftAsync(
        Guid applicationId,
        Guid requesterUserId,
        bool isAdmin,
        SaveLoanApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedOrAdminApplicationAsync(
            applicationId, requesterUserId, isAdmin, cancellationToken);

        // Admins bypass the editable-status check; applicants must be in an editable status
        if (!isAdmin && !ApplicationStatuses.EditableStatuses.Contains(application.Status))
        {
            throw new InvalidOperationException(
                $"Application cannot be updated in status '{application.Status}'. " +
                $"Allowed statuses: {string.Join(", ", ApplicationStatuses.EditableStatuses)}.");
        }

        if (application.Status == ApplicationStatuses.Withdrawn)
            throw new InvalidOperationException("Withdrawn applications cannot be updated.");

        var now = DateTime.UtcNow;
        var oldSnapshot = Snapshot(application);

        ApplyRequest(application, request, now);

        // Track edit metadata (only for applicant edits, not admin overrides)
        if (!isAdmin)
        {
            application.IsEdited = true;
            application.EditCount += 1;
            application.LastModifiedAt = now;
            application.LastModifiedBy = requesterUserId.ToString();
        }

        // Append history record
        application.History.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ChangedBy = requesterUserId,
            ChangedAt = now,
            Action = "Updated",
            OldData = oldSnapshot,
            NewData = Snapshot(application)
        });

        await _loanApplicationRepository.UpdateAsync(application, cancellationToken);
        await _cache.RemoveAsync($"apps:user:{application.ApplicantUserId}", cancellationToken);
        return Map(application);
    }

    // ── Withdraw ──────────────────────────────────────────────────────────────

    public async Task<LoanApplicationResponse> WithdrawAsync(
        Guid applicationId,
        Guid applicantUserId,
        WithdrawApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedOrAdminApplicationAsync(
            applicationId, applicantUserId, false, cancellationToken);

        if (!ApplicationStatuses.WithdrawableStatuses.Contains(application.Status))
        {
            throw new InvalidOperationException(
                $"Application cannot be withdrawn in status '{application.Status}'. " +
                "Withdrawal is only allowed before the application reaches 'Under Review'.");
        }

        var now = DateTime.UtcNow;
        var previousStatus = application.Status;

        application.Status = ApplicationStatuses.Withdrawn;
        application.WithdrawalReason = request.Reason?.Trim();
        application.WithdrawnAtUtc = now;
        application.UpdatedAtUtc = now;

        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            LoanApplicationId = application.Id,
            FromStatus = previousStatus,
            ToStatus = ApplicationStatuses.Withdrawn,
            Remarks = string.IsNullOrWhiteSpace(request.Reason)
                ? "Application withdrawn by applicant."
                : $"Application withdrawn. Reason: {request.Reason.Trim()}",
            ChangedByUserId = applicantUserId,
            ChangedAtUtc = now
        });

        application.History.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ChangedBy = applicantUserId,
            ChangedAt = now,
            Action = "Withdrawn",
            OldData = null,
            NewData = JsonSerializer.Serialize(new { status = ApplicationStatuses.Withdrawn, reason = request.Reason }, _jsonOptions)
        });

        await _loanApplicationRepository.UpdateAsync(application, cancellationToken);
        await _cache.RemoveAsync($"apps:user:{application.ApplicantUserId}", cancellationToken);
        return Map(application);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<LoanApplicationResponse> GetByIdAsync(
        Guid applicationId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedOrAdminApplicationAsync(
            applicationId, requesterUserId, isAdmin, cancellationToken);
        return Map(application);
    }

    public async Task<IReadOnlyCollection<LoanApplicationResponse>> GetMineAsync(
        Guid applicantUserId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"apps:user:{applicantUserId}";
        var cached = await _cache.GetAsync<LoanApplicationResponse[]>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var applications = await _loanApplicationRepository
            .GetByApplicantUserIdAsync(applicantUserId, cancellationToken);

        var result = applications
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(Map)
            .ToArray();

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return result;
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    public async Task<LoanApplicationResponse> SubmitAsync(
        Guid applicationId,
        Guid requesterUserId,
        CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedOrAdminApplicationAsync(
            applicationId, requesterUserId, false, cancellationToken);

        if (!string.Equals(application.Status, ApplicationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only draft applications can be submitted.");

        ValidateForSubmission(application);

        var previousStatus = application.Status;
        var now = DateTime.UtcNow;
        application.Status = ApplicationStatuses.Submitted;
        application.SubmittedAtUtc = now;
        application.UpdatedAtUtc = now;

        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            LoanApplicationId = application.Id,
            FromStatus = previousStatus,
            ToStatus = ApplicationStatuses.Submitted,
            Remarks = "Application submitted by applicant.",
            ChangedByUserId = requesterUserId,
            ChangedAtUtc = now
        });

        await _loanApplicationRepository.UpdateAsync(application, cancellationToken);
        await _cache.RemoveAsync($"apps:user:{application.ApplicantUserId}", cancellationToken);

        await _eventPublisher.PublishAsync(new ApplicationSubmittedEvent
        {
            ApplicationId = application.Id,
            ApplicantUserId = application.ApplicantUserId,
            ApplicationNumber = application.ApplicationNumber,
            ApplicantName = $"{application.FirstName} {application.LastName}".Trim(),
            Email = application.Email,
            RequestedAmount = application.RequestedAmount,
            RequestedTenureMonths = application.RequestedTenureMonths,
            SubmittedAtUtc = now
        }, cancellationToken);

        return Map(application);
    }

    // ── Status timeline ───────────────────────────────────────────────────────

    public async Task<LoanApplicationStatusResponse> GetStatusAsync(
        Guid applicationId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedOrAdminApplicationAsync(
            applicationId, requesterUserId, isAdmin, cancellationToken);

        return new LoanApplicationStatusResponse
        {
            Id = application.Id,
            ApplicationNumber = application.ApplicationNumber,
            CurrentStatus = application.Status,
            Timeline = application.StatusHistory
                .OrderBy(x => x.ChangedAtUtc)
                .Select(x => new ApplicationStatusHistoryResponse
                {
                    FromStatus = x.FromStatus,
                    ToStatus = x.ToStatus,
                    Remarks = x.Remarks,
                    ChangedByUserId = x.ChangedByUserId,
                    ChangedAtUtc = x.ChangedAtUtc
                })
                .ToArray()
        };
    }

    // ── Delete (Draft only) ───────────────────────────────────────────────────

    public async Task DeleteDraftAsync(
        Guid applicationId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var application = await GetOwnedOrAdminApplicationAsync(
            applicationId, requesterUserId, isAdmin, cancellationToken);

        if (!string.Equals(application.Status, ApplicationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only draft applications can be deleted.");

        await _loanApplicationRepository.DeleteAsync(application, cancellationToken);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<LoanApplication> GetOwnedOrAdminApplicationAsync(
        Guid applicationId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var application = await _loanApplicationRepository.GetByIdAsync(applicationId, cancellationToken)
                         ?? throw new KeyNotFoundException("Application not found.");

        if (!isAdmin && application.ApplicantUserId != requesterUserId)
            throw new UnauthorizedAccessException("You are not allowed to access this application.");

        return application;
    }

    private static void ApplyRequest(
        LoanApplication application,
        SaveLoanApplicationRequest request,
        DateTime updatedAtUtc)
    {
        application.FirstName = (request.PersonalDetails.FirstName ?? string.Empty).Trim();
        application.LastName = (request.PersonalDetails.LastName ?? string.Empty).Trim();
        application.DateOfBirth = request.PersonalDetails.DateOfBirth;
        application.Gender = (request.PersonalDetails.Gender ?? string.Empty).Trim();
        application.Email = (request.PersonalDetails.Email ?? string.Empty).Trim();
        application.Phone = (request.PersonalDetails.Phone ?? string.Empty).Trim();
        application.AddressLine1 = (request.PersonalDetails.AddressLine1 ?? string.Empty).Trim();
        application.AddressLine2 = (request.PersonalDetails.AddressLine2 ?? string.Empty).Trim();
        application.City = (request.PersonalDetails.City ?? string.Empty).Trim();
        application.State = (request.PersonalDetails.State ?? string.Empty).Trim();
        application.PostalCode = (request.PersonalDetails.PostalCode ?? string.Empty).Trim();
        application.EmployerName = (request.EmploymentDetails.EmployerName ?? string.Empty).Trim();
        application.EmploymentType = (request.EmploymentDetails.EmploymentType ?? string.Empty).Trim();
        application.MonthlyIncome = request.EmploymentDetails.MonthlyIncome;
        application.AnnualIncome = request.EmploymentDetails.AnnualIncome;
        application.ExistingEmiAmount = request.EmploymentDetails.ExistingEmiAmount;
        application.RequestedAmount = request.LoanDetails.RequestedAmount;
        application.RequestedTenureMonths = request.LoanDetails.RequestedTenureMonths;
        application.LoanPurpose = (request.LoanDetails.LoanPurpose ?? string.Empty).Trim();
        application.Remarks = (request.LoanDetails.Remarks ?? string.Empty).Trim();
        application.UpdatedAtUtc = updatedAtUtc;
    }

    private static void ValidateForSubmission(LoanApplication application)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(application.FirstName))    missing.Add(nameof(application.FirstName));
        if (string.IsNullOrWhiteSpace(application.LastName))     missing.Add(nameof(application.LastName));
        if (application.DateOfBirth is null)                     missing.Add(nameof(application.DateOfBirth));
        if (string.IsNullOrWhiteSpace(application.Email))        missing.Add(nameof(application.Email));
        if (string.IsNullOrWhiteSpace(application.Phone))        missing.Add(nameof(application.Phone));
        if (string.IsNullOrWhiteSpace(application.AddressLine1)) missing.Add(nameof(application.AddressLine1));
        if (string.IsNullOrWhiteSpace(application.City))         missing.Add(nameof(application.City));
        if (string.IsNullOrWhiteSpace(application.State))        missing.Add(nameof(application.State));
        if (string.IsNullOrWhiteSpace(application.PostalCode))   missing.Add(nameof(application.PostalCode));
        if (string.IsNullOrWhiteSpace(application.EmployerName)) missing.Add(nameof(application.EmployerName));
        if (string.IsNullOrWhiteSpace(application.EmploymentType)) missing.Add(nameof(application.EmploymentType));
        if (application.MonthlyIncome is null or <= 0)           missing.Add(nameof(application.MonthlyIncome));
        if (application.AnnualIncome  is null or <= 0)           missing.Add(nameof(application.AnnualIncome));
        if (application.RequestedAmount <= 0)                    missing.Add(nameof(application.RequestedAmount));
        if (application.RequestedTenureMonths <= 0)              missing.Add(nameof(application.RequestedTenureMonths));
        if (string.IsNullOrWhiteSpace(application.LoanPurpose))  missing.Add(nameof(application.LoanPurpose));

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Application is incomplete. Missing or invalid: {string.Join(", ", missing)}.");

        if (application.RequestedAmount is < 10_000 or > 5_000_000)
            throw new InvalidOperationException("Requested amount must be between 10,000 and 5,000,000.");

        if (application.RequestedTenureMonths is < 6 or > 360)
            throw new InvalidOperationException("Requested tenure must be between 6 and 360 months.");

        if (application.MonthlyIncome <= application.ExistingEmiAmount)
            throw new InvalidOperationException("Monthly income must be greater than existing EMI obligations.");
    }

    /// <summary>Serialises the application to a compact JSON string for history snapshots.</summary>
    private static string Snapshot(LoanApplication app) =>
        JsonSerializer.Serialize(new
        {
            app.Status,
            app.FirstName, app.LastName, app.Email, app.Phone,
            app.AddressLine1, app.City, app.State, app.PostalCode,
            app.EmployerName, app.EmploymentType,
            app.MonthlyIncome, app.AnnualIncome, app.ExistingEmiAmount,
            app.RequestedAmount, app.RequestedTenureMonths, app.LoanPurpose,
            app.UpdatedAtUtc
        }, _jsonOptions);

    private static LoanApplicationResponse Map(LoanApplication application) =>
        new()
        {
            Id = application.Id,
            ApplicationNumber = application.ApplicationNumber,
            ApplicantUserId = application.ApplicantUserId,
            Status = application.Status,
            PersonalDetails = new PersonalDetailsResponse
            {
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
                PostalCode = application.PostalCode
            },
            EmploymentDetails = new EmploymentDetailsResponse
            {
                EmployerName = application.EmployerName,
                EmploymentType = application.EmploymentType,
                MonthlyIncome = application.MonthlyIncome,
                AnnualIncome = application.AnnualIncome,
                ExistingEmiAmount = application.ExistingEmiAmount
            },
            LoanDetails = new LoanDetailsResponse
            {
                RequestedAmount = application.RequestedAmount,
                RequestedTenureMonths = application.RequestedTenureMonths,
                LoanPurpose = application.LoanPurpose,
                Remarks = application.Remarks
            },
            CreatedAtUtc = application.CreatedAtUtc,
            UpdatedAtUtc = application.UpdatedAtUtc,
            SubmittedAtUtc = application.SubmittedAtUtc,
            // Edit tracking
            IsEdited = application.IsEdited,
            EditCount = application.EditCount,
            LastModifiedAt = application.LastModifiedAt,
            LastModifiedBy = application.LastModifiedBy,
            // Withdrawal
            WithdrawalReason = application.WithdrawalReason,
            WithdrawnAtUtc = application.WithdrawnAtUtc
        };
}

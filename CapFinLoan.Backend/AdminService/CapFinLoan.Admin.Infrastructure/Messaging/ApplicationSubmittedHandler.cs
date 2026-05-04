using CapFinLoan.Admin.Domain.Constants;
using CapFinLoan.Admin.Domain.Entities;
using CapFinLoan.Admin.Persistence.Data;
using CapFinLoan.Api.Shared.Caching;
using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Admin.Infrastructure.Messaging;

/// <summary>
/// When an application is submitted, upsert it into the admin DB so it
/// appears in the admin queue immediately — no polling required.
/// </summary>
public sealed class ApplicationSubmittedHandler : IMessageHandler<ApplicationSubmittedEvent>
{
    private readonly AdminDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<ApplicationSubmittedHandler> _logger;

    public ApplicationSubmittedHandler(
        AdminDbContext db,
        ICacheService cache,
        ILogger<ApplicationSubmittedHandler> logger)
    {
        _db     = db;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<MessageAcknowledgment> HandleAsync(
        ApplicationSubmittedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[AdminService] ApplicationSubmitted — {ApplicationNumber}, " +
            "Applicant: {ApplicantName} ({Email}), Amount: {Amount:C}, " +
            "Tenure: {Tenure} months, SubmittedAt: {SubmittedAt:u}",
            message.ApplicationNumber, message.ApplicantName, message.Email,
            message.RequestedAmount, message.RequestedTenureMonths, message.SubmittedAtUtc);

        try
        {
            // ── Upsert into admin DB ──────────────────────────────────────────
            var existing = await _db.LoanApplications
                .FirstOrDefaultAsync(x => x.Id == message.ApplicationId, cancellationToken);

            if (existing is null)
            {
                // Parse first/last name from ApplicantName (e.g. "Arjun Sharma")
                var nameParts = (message.ApplicantName ?? "").Trim().Split(' ', 2);
                var firstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
                var lastName  = nameParts.Length > 1 ? nameParts[1] : string.Empty;

                var app = new LoanApplication
                {
                    Id                    = message.ApplicationId,
                    ApplicantUserId       = message.ApplicantUserId,
                    ApplicationNumber     = message.ApplicationNumber,
                    Status                = ApplicationStatuses.Submitted,
                    FirstName             = firstName,
                    LastName              = lastName,
                    Email                 = message.Email,
                    RequestedAmount       = message.RequestedAmount,
                    RequestedTenureMonths = message.RequestedTenureMonths,
                    SubmittedAtUtc        = message.SubmittedAtUtc,
                    CreatedAtUtc          = message.SubmittedAtUtc,
                    UpdatedAtUtc          = message.SubmittedAtUtc
                };

                app.StatusHistory.Add(new ApplicationStatusHistory
                {
                    LoanApplicationId = app.Id,
                    FromStatus        = ApplicationStatuses.Draft,
                    ToStatus          = ApplicationStatuses.Submitted,
                    Remarks           = "Application submitted by applicant.",
                    ChangedByUserId   = message.ApplicantUserId,
                    ChangedAtUtc      = message.SubmittedAtUtc
                });

                _db.LoanApplications.Add(app);
            }
            else if (!string.Equals(existing.Status, ApplicationStatuses.Submitted,
                         StringComparison.OrdinalIgnoreCase))
            {
                // Already exists but status is stale — update to Submitted
                existing.Status        = ApplicationStatuses.Submitted;
                existing.SubmittedAtUtc = message.SubmittedAtUtc;
                existing.UpdatedAtUtc  = message.SubmittedAtUtc;
            }

            await _db.SaveChangesAsync(cancellationToken);

            // ── Invalidate admin caches ───────────────────────────────────────
            await _cache.RemoveByPrefixAsync("admin:queue:", cancellationToken);
            await _cache.RemoveAsync("admin:dashboard", cancellationToken);

            _logger.LogInformation(
                "[AdminService] Application {ApplicationNumber} upserted into admin DB.",
                message.ApplicationNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[AdminService] Failed to upsert application {ApplicationNumber} into admin DB.",
                message.ApplicationNumber);
            // Return Nack so RabbitMQ retries
            return MessageAcknowledgment.NackRequeue("DB upsert failed — will retry");
        }

        return MessageAcknowledgment.Ack();
    }
}

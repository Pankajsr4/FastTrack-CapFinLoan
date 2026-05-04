using CapFinLoan.Api.Shared.Caching;
using CapFinLoan.Application.Application.Interfaces;
using CapFinLoan.Application.Domain.Entities;
using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Application.Infrastructure.Messaging;

/// <summary>
/// When the admin changes an application's status, update the ApplicationService DB
/// so the applicant sees the correct status immediately.
/// </summary>
public sealed class ApplicationStatusChangedHandler : IMessageHandler<ApplicationStatusChangedEvent>
{
    private readonly ILoanApplicationRepository _repo;
    private readonly ICacheService _cache;
    private readonly ILogger<ApplicationStatusChangedHandler> _logger;

    public ApplicationStatusChangedHandler(
        ILoanApplicationRepository repo,
        ICacheService cache,
        ILogger<ApplicationStatusChangedHandler> logger)
    {
        _repo   = repo;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<MessageAcknowledgment> HandleAsync(
        ApplicationStatusChangedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[ApplicationService] ApplicationStatusChanged — {ApplicationNumber}: {Previous} → {New}",
            message.ApplicationNumber, message.PreviousStatus, message.NewStatus);

        try
        {
            var application = await _repo.GetByIdAsync(message.ApplicationId, cancellationToken);

            if (application is null)
            {
                _logger.LogWarning(
                    "[ApplicationService] Application {ApplicationId} not found — cannot update status.",
                    message.ApplicationId);
                // Ack anyway — this service may not own this application
                return MessageAcknowledgment.Ack();
            }

            // Only update if the status actually changed (idempotency guard)
            if (string.Equals(application.Status, message.NewStatus, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "[ApplicationService] Application {ApplicationNumber} already in status '{Status}' — skipping.",
                    message.ApplicationNumber, message.NewStatus);
                return MessageAcknowledgment.Ack();
            }

            var previousStatus = application.Status;
            application.Status       = message.NewStatus;
            application.UpdatedAtUtc = message.ChangedAtUtc;

            // Add status history entry
            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                LoanApplicationId = application.Id,
                FromStatus        = previousStatus,
                ToStatus          = message.NewStatus,
                Remarks           = message.Remarks ?? $"Status updated by admin.",
                ChangedByUserId   = message.ChangedByUserId,
                ChangedAtUtc      = message.ChangedAtUtc
            });

            await _repo.UpdateAsync(application, cancellationToken);

            // Invalidate the user's cached application list
            await _cache.RemoveAsync($"apps:user:{application.ApplicantUserId}", cancellationToken);

            _logger.LogInformation(
                "[ApplicationService] Application {ApplicationNumber} status updated: {Previous} → {New}",
                message.ApplicationNumber, previousStatus, message.NewStatus);

            return MessageAcknowledgment.Ack();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ApplicationService] Failed to update status for {ApplicationNumber}",
                message.ApplicationNumber);
            return MessageAcknowledgment.NackRequeue("DB update failed — will retry");
        }
    }
}

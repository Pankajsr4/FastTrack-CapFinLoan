using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using CapFinLoan.Notification.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Notification.Infrastructure.Messaging;

public sealed class ApplicationStatusChangedHandler : IMessageHandler<ApplicationStatusChangedEvent>
{
    private const string Approved = "Approved";
    private const string Rejected = "Rejected";

    private readonly INotificationService _notifications;
    private readonly ILogger<ApplicationStatusChangedHandler> _logger;

    public ApplicationStatusChangedHandler(
        INotificationService notifications,
        ILogger<ApplicationStatusChangedHandler> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<MessageAcknowledgment> HandleAsync(
        ApplicationStatusChangedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "[NotificationService] Handling ApplicationStatusChanged for {Ref}: {From} → {To}",
            message.ApplicationNumber, message.PreviousStatus, message.NewStatus);

        if (string.Equals(message.NewStatus, Approved, StringComparison.OrdinalIgnoreCase))
        {
            await _notifications.SendApplicationApprovedAsync(
                message.ApplicationId,
                message.ApplicantUserId,                          // ← now carried in event
                message.ApplicationNumber,
                applicantName: $"Applicant ({message.ApplicantUserId})",
                email: $"applicant-{message.ApplicantUserId}@capfinloan.internal",
                amount: 0,
                cancellationToken);
        }
        else if (string.Equals(message.NewStatus, Rejected, StringComparison.OrdinalIgnoreCase))
        {
            await _notifications.SendApplicationRejectedAsync(
                message.ApplicationId,
                message.ApplicantUserId,                          // ← now carried in event
                message.ApplicationNumber,
                applicantName: $"Applicant ({message.ApplicantUserId})",
                email: $"applicant-{message.ApplicantUserId}@capfinloan.internal",
                remarks: message.Remarks,
                cancellationToken);
        }
        else
        {
            await _notifications.SendApplicationUpdatedAsync(
                message.ApplicationId,
                message.ApplicantUserId,                          // ← now carried in event
                message.ApplicationNumber,
                message.PreviousStatus,
                message.NewStatus,
                message.Remarks,
                cancellationToken);
        }

        return MessageAcknowledgment.Ack();
    }
}

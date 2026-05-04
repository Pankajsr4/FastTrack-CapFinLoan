using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using CapFinLoan.Notification.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Notification.Infrastructure.Messaging;

public sealed class ApplicationSubmittedHandler : IMessageHandler<ApplicationSubmittedEvent>
{
    private readonly INotificationService _notifications;
    private readonly ILogger<ApplicationSubmittedHandler> _logger;

    public ApplicationSubmittedHandler(
        INotificationService notifications,
        ILogger<ApplicationSubmittedHandler> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<MessageAcknowledgment> HandleAsync(
        ApplicationSubmittedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "[NotificationService] Handling ApplicationSubmitted for {Ref}",
            message.ApplicationNumber);

        await _notifications.SendApplicationSubmittedAsync(
            message.ApplicationId,
            message.ApplicantUserId,
            message.ApplicationNumber,
            message.ApplicantName,
            message.Email,
            message.RequestedAmount,
            cancellationToken);

        return MessageAcknowledgment.Ack();
    }
}

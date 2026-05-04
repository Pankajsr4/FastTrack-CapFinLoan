using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Document.Infrastructure.Messaging;

/// <summary>
/// Handles ApplicationStatusChangedEvent in the DocumentService.
/// Reacts when a loan application is approved or rejected so documents
/// can be locked / archived accordingly.
/// </summary>
public sealed class ApplicationStatusChangedHandler : IMessageHandler<ApplicationStatusChangedEvent>
{
    private readonly ILogger<ApplicationStatusChangedHandler> _logger;

    // TODO: inject IDocumentRepository when ready to persist state changes
    public ApplicationStatusChangedHandler(ILogger<ApplicationStatusChangedHandler> logger)
    {
        _logger = logger;
    }

    public Task<MessageAcknowledgment> HandleAsync(
        ApplicationStatusChangedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DocumentService] ApplicationStatusChanged — Application: {ApplicationNumber}, " +
            "Status: {Previous} → {New}, ChangedBy: {UserId}",
            message.ApplicationNumber, message.PreviousStatus,
            message.NewStatus, message.ChangedByUserId);

        // TODO: If NewStatus is "Approved" or "Rejected", mark all documents for this
        // application as read-only / archived via IDocumentRepository.

        return Task.FromResult(MessageAcknowledgment.Ack());
    }
}

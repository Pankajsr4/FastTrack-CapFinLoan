using CapFinLoan.Messaging.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Document.Infrastructure.Messaging;

/// <summary>
/// Listens for application status changes so the document service can react
/// (e.g., lock documents when an application is approved/rejected).
/// </summary>
public class ApplicationStatusChangedConsumer : IConsumer<ApplicationStatusChangedEvent>
{
    private readonly ILogger<ApplicationStatusChangedConsumer> _logger;

    public ApplicationStatusChangedConsumer(ILogger<ApplicationStatusChangedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ApplicationStatusChangedEvent> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "[DocumentService] ApplicationStatusChanged — Application: {ApplicationNumber}, Status: {Previous} → {New}, ChangedBy: {UserId}",
            msg.ApplicationNumber, msg.PreviousStatus, msg.NewStatus, msg.ChangedByUserId);

        // TODO: If NewStatus is "Approved" or "Rejected", mark all documents for this
        // application as read-only / archived via IDocumentRepository.

        return Task.CompletedTask;
    }
}

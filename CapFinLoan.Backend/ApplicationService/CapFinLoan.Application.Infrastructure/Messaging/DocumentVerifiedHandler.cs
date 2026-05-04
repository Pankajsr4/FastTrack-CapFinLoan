using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Application.Infrastructure.Messaging;

/// <summary>
/// Reacts to document verification outcomes so the application service can
/// advance or block the loan application workflow accordingly.
/// </summary>
public sealed class DocumentVerifiedHandler : IMessageHandler<DocumentVerifiedEvent>
{
    private readonly ILogger<DocumentVerifiedHandler> _logger;

    // TODO: inject ILoanApplicationService when ready to advance application status
    public DocumentVerifiedHandler(ILogger<DocumentVerifiedHandler> logger)
    {
        _logger = logger;
    }

    public Task<MessageAcknowledgment> HandleAsync(DocumentVerifiedEvent message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[ApplicationService] DocumentVerified — Application: {ApplicationId}, " +
            "Document: {DocumentId}, Type: {DocumentType}, Verified: {IsVerified}, Remarks: {Remarks}",
            message.ApplicationId, message.DocumentId, message.DocumentType,
            message.IsVerified, message.Remarks ?? "N/A");

        // TODO: Check if all required documents for the application are now verified.
        // If yes, auto-advance application status to "DocumentsComplete" via ILoanApplicationService.

        return Task.FromResult(MessageAcknowledgment.Ack());
    }
}

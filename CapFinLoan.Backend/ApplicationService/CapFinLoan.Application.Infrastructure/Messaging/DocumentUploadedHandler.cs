using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Application.Infrastructure.Messaging;

/// <summary>
/// Tracks document uploads against loan applications so the application
/// service knows which required documents have been submitted.
/// </summary>
public sealed class DocumentUploadedHandler : IMessageHandler<DocumentUploadedEvent>
{
    private readonly ILogger<DocumentUploadedHandler> _logger;

    // TODO: inject ILoanApplicationRepository when ready to persist state changes
    public DocumentUploadedHandler(ILogger<DocumentUploadedHandler> logger)
    {
        _logger = logger;
    }

    public Task<MessageAcknowledgment> HandleAsync(DocumentUploadedEvent message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[ApplicationService] DocumentUploaded — Application: {ApplicationId}, " +
            "Document: {DocumentId}, Type: {DocumentType}",
            message.ApplicationId, message.DocumentId, message.DocumentType);

        // TODO: Update application checklist / required-documents tracking via ILoanApplicationRepository.

        return Task.FromResult(MessageAcknowledgment.Ack());
    }
}

using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Admin.Infrastructure.Messaging;

public sealed class DocumentVerifiedHandler : IMessageHandler<DocumentVerifiedEvent>
{
    private readonly ILogger<DocumentVerifiedHandler> _logger;

    public DocumentVerifiedHandler(ILogger<DocumentVerifiedHandler> logger)
    {
        _logger = logger;
    }

    public Task<MessageAcknowledgment> HandleAsync(DocumentVerifiedEvent message, CancellationToken cancellationToken)
    {
        var status = message.IsVerified ? "VERIFIED" : "REJECTED";

        _logger.LogInformation(
            "[AdminService] DocumentVerified — Document: {FileName} ({DocumentType}) [{Status}], " +
            "Application: {ApplicationId}, VerifiedBy: {VerifiedByUserId}, " +
            "At: {VerifiedAt:u}, Remarks: {Remarks}",
            message.FileName, message.DocumentType, status, message.ApplicationId,
            message.VerifiedByUserId, message.VerifiedAtUtc, message.Remarks ?? "N/A");

        return Task.FromResult(MessageAcknowledgment.Ack());
    }
}

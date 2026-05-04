namespace CapFinLoan.Document.Application.Interfaces;

/// <summary>
/// Abstraction over SignalR push — injected into DocumentService to notify
/// connected clients when a document status changes.
/// Implemented by DocumentStatusNotifier in the API layer.
/// </summary>
public interface IDocumentStatusNotifier
{
    Task NotifyStatusChangedAsync(
        Guid    documentId,
        string  status,
        string? failureReason = null,
        CancellationToken ct  = default);
}

using CapFinLoan.Messaging.Contracts.Events;

namespace CapFinLoan.Admin.Application.Interfaces;

/// <summary>
/// Orchestrates the processing pipeline for a newly uploaded document.
///
/// Called directly from the RabbitMQ message handler with the raw event —
/// no intermediate DTO mapping required.
///
/// Responsibilities:
///   1. Validate the document metadata (type, size, content-type).
///   2. Log the processing start with all relevant fields.
///   3. Transition the document status to UnderReview via DocumentService.
///   4. (Future) Persist a review queue entry, send admin notification,
///      trigger automated pre-screening (virus scan, OCR, etc.).
///
/// Throws on validation failure or downstream errors — the caller
/// (RabbitMqConsumer via DocumentUploadedHandler) is responsible for nack/retry.
/// </summary>
public interface IDocumentProcessingService
{
    /// <summary>
    /// Begins processing the document described by <paramref name="evt"/>.
    /// </summary>
    Task ProcessDocumentAsync(DocumentUploadedEvent evt, CancellationToken cancellationToken = default);
}

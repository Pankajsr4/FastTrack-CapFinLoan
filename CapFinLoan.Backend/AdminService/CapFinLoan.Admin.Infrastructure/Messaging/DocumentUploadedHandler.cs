using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Admin.Infrastructure.Messaging;

/// <summary>
/// Handles DocumentUploadedEvent consumed from the "document-uploaded-event" queue.
///
/// ── IServiceScopeFactory pattern ────────────────────────────────────────────
///
/// This handler is registered as Scoped and resolved from a scope created by
/// RabbitMqConsumer&lt;T&gt;. However, it explicitly uses IServiceScopeFactory to
/// create its own child scope for IDocumentProcessingService resolution.
///
/// Why an explicit inner scope?
///   - Makes the scope boundary and lifetime of IDocumentProcessingService
///     visible and intentional at the call site.
///   - IDocumentProcessingService depends on IDocumentProcessingRepository,
///     which depends on AdminDbContext (Scoped). The inner scope ensures each
///     ProcessDocumentAsync call gets its own DbContext and unit of work,
///     even if the handler itself is reused across multiple messages in future.
///   - Disposing the scope triggers DisposeAsync on AdminDbContext, returning
///     the DB connection to the pool immediately after processing completes.
///
/// Scope lifetime = one message delivery.
///
/// On exception: RabbitMqConsumer nacks with requeue → retried up to MaxDeliveryCount,
/// then dead-lettered to "document-uploaded-event.dlq".
/// </summary>
public sealed class DocumentUploadedHandler : IMessageHandler<DocumentUploadedEvent>
{
    // IServiceScopeFactory is Singleton-safe — it creates child scopes on demand
    // without holding any scoped state itself.
    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ILogger<DocumentUploadedHandler> _logger;

    public DocumentUploadedHandler(
        IServiceScopeFactory          scopeFactory,
        ILogger<DocumentUploadedHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public async Task<MessageAcknowledgment> HandleAsync(
        DocumentUploadedEvent message,
        CancellationToken cancellationToken)
    {
        var eventData = message;

        // ── Log: message received ─────────────────────────────────────────────
        // Information level — visible in production without Debug logging enabled.
        // Includes all key fields so the message can be traced without a DB query.
        _logger.LogInformation(
            "[DocumentUploadedHandler] Message received — " +
            "DocumentId: {DocumentId} | ApplicationId: {ApplicationId} | UserId: {UserId} | " +
            "Type: {DocumentType} | File: {FileName} ({ContentType}, {SizeKb:F1} KB) | " +
            "UploadedAt: {UploadedAt:u}",
            eventData.DocumentId,
            eventData.ApplicationId,
            eventData.UserId,
            eventData.DocumentType,
            eventData.FileName,
            eventData.ContentType,
            eventData.FileSizeBytes / 1024.0,
            eventData.UploadedAtUtc);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IDocumentProcessingService>();

        try
        {
            await service.ProcessDocumentAsync(eventData, cancellationToken);

            // ── Log: processing result — SUCCESS ──────────────────────────────
            _logger.LogInformation(
                "[DocumentUploadedHandler] Processing result: SUCCESS — " +
                "DocumentId: {DocumentId} → Ack",
                eventData.DocumentId);

            return MessageAcknowledgment.Ack();
        }
        catch (ArgumentException ex)
        {
            // ── Log: processing result — PERMANENT FAILURE (validation) ───────
            _logger.LogError(ex,
                "[DocumentUploadedHandler] Processing result: PERMANENT FAILURE — " +
                "DocumentId: {DocumentId} | Reason: {Reason} → NackDiscard (DLQ)",
                eventData.DocumentId, ex.Message);

            return MessageAcknowledgment.NackDiscard($"Validation failed: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            // ── Log: processing result — PERMANENT FAILURE (business rule) ────
            _logger.LogError(ex,
                "[DocumentUploadedHandler] Processing result: PERMANENT FAILURE — " +
                "DocumentId: {DocumentId} | Reason: {Reason} → NackDiscard (DLQ)",
                eventData.DocumentId, ex.Message);

            return MessageAcknowledgment.NackDiscard($"Business rule violation: {ex.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ── Log: processing result — SHUTDOWN ─────────────────────────────
            _logger.LogWarning(
                "[DocumentUploadedHandler] Processing result: SHUTDOWN — " +
                "DocumentId: {DocumentId} → NackRequeue",
                eventData.DocumentId);

            return MessageAcknowledgment.NackRequeue("Host shutdown");
        }
        catch (Exception ex)
        {
            // ── Log: processing result — TRANSIENT FAILURE ────────────────────
            _logger.LogError(ex,
                "[DocumentUploadedHandler] Processing result: TRANSIENT FAILURE — " +
                "DocumentId: {DocumentId} | {ExceptionType}: {Reason} → NackRequeue (retry)",
                eventData.DocumentId, ex.GetType().Name, ex.Message);

            return MessageAcknowledgment.NackRequeue($"{ex.GetType().Name}: {ex.Message}");
        }
        // scope.DisposeAsync() called here by await using
    }
}

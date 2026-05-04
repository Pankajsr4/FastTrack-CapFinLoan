using CapFinLoan.Document.Application.Interfaces;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Document.Infrastructure.Messaging;

/// <summary>
/// IEventPublisher implementation for DocumentService.
/// Wraps RabbitMqPublisher and adds structured log events:
///   - Message received (publish requested)
///   - Processing started (serialization + queue declaration)
///   - Processing completed (published successfully)
///   - Errors (publish failed)
/// </summary>
public sealed class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqPublisher _publisher;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(
        RabbitMqPublisher publisher,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        var eventType = typeof(T).Name;
        var queueName = ToQueueName(eventType);

        // ── Message received (publish requested) ──────────────────────────────
        _logger.LogInformation(
            "[DocumentService] Publish requested — EventType: {EventType} → Queue: '{Queue}'",
            eventType, queueName);

        try
        {
            // ── Processing started ────────────────────────────────────────────
            _logger.LogDebug(
                "[DocumentService] Publishing {EventType} — serializing and declaring queue '{Queue}'",
                eventType, queueName);

            await _publisher.PublishAsync(queueName, message, cancellationToken);

            // ── Processing completed ──────────────────────────────────────────
            _logger.LogInformation(
                "[DocumentService] Published {EventType} → '{Queue}' successfully.",
                eventType, queueName);
        }
        catch (Exception ex)
        {
            // ── Error ─────────────────────────────────────────────────────────
            _logger.LogError(ex,
                "[DocumentService] Failed to publish {EventType} → '{Queue}': {ExceptionType}: {Message}",
                eventType, queueName, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    private static string ToQueueName(string typeName) =>
        string.Concat(typeName.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? $"-{char.ToLowerInvariant(c)}" : char.ToLowerInvariant(c).ToString()));
}

namespace CapFinLoan.Messaging.Contracts.Messaging;

/// <summary>
/// Handles a specific message type consumed from RabbitMQ.
/// One implementation per event type per service.
/// Registered as Scoped — a new instance is created per message via IServiceScopeFactory.
///
/// ── Acknowledgment contract ──────────────────────────────────────────────────
///
/// The handler returns a MessageAcknowledgment that explicitly controls what
/// RabbitMqConsumer&lt;T&gt; does with the AMQP delivery after HandleAsync completes:
///
///   MessageAcknowledgment.Ack()
///     → BasicAck — message permanently removed from the queue.
///
///   MessageAcknowledgment.NackRequeue(reason)
///     → BasicNack(requeue: true) — message returned to queue for retry.
///     → Use for transient failures: downstream unavailable, DB timeout, etc.
///     → After MaxDeliveryCount retries the consumer dead-letters automatically.
///
///   MessageAcknowledgment.NackDiscard(reason)
///     → BasicNack(requeue: false) — message routed to DLQ immediately.
///     → Use for permanent failures: validation error, unsupported type, etc.
///
/// Throwing an exception is still supported as a fallback — the consumer
/// classifies it as transient or permanent based on the exception type.
/// Prefer returning an explicit acknowledgment over throwing when the outcome
/// is known at the handler level.
/// </summary>
public interface IMessageHandler<in T> where T : class
{
    Task<MessageAcknowledgment> HandleAsync(T message, CancellationToken cancellationToken);
}

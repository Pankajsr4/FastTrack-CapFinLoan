namespace CapFinLoan.Messaging.Contracts.Messaging;

/// <summary>
/// Represents the explicit acknowledgment decision returned by IMessageHandler&lt;T&gt;.
///
/// RabbitMqConsumer&lt;T&gt; reads this result and calls the corresponding AMQP method:
///   Ack         → BasicAck   — message permanently removed from the queue
///   NackRequeue → BasicNack(requeue: true)  — returned to queue for retry
///   NackDiscard → BasicNack(requeue: false) — routed to DLQ, never retried
/// </summary>
public sealed class MessageAcknowledgment
{
    private MessageAcknowledgment(AckDecision decision, string? reason = null)
    {
        Decision = decision;
        Reason   = reason;
    }

    public AckDecision Decision { get; }

    /// <summary>Optional human-readable reason — logged by the consumer.</summary>
    public string? Reason { get; }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Processing succeeded — BasicAck the message.
    /// </summary>
    public static MessageAcknowledgment Ack() =>
        new(AckDecision.Ack);

    /// <summary>
    /// Transient failure — BasicNack with requeue=true.
    /// The message will be retried up to MaxDeliveryCount times.
    /// Use for: downstream service unavailable, DB timeout, network blip.
    /// </summary>
    public static MessageAcknowledgment NackRequeue(string reason) =>
        new(AckDecision.NackRequeue, reason);

    /// <summary>
    /// Permanent failure — BasicNack with requeue=false → routed to DLQ.
    /// Use for: validation errors, malformed data, unsupported document type.
    /// </summary>
    public static MessageAcknowledgment NackDiscard(string reason) =>
        new(AckDecision.NackDiscard, reason);
}

public enum AckDecision
{
    Ack,
    NackRequeue,
    NackDiscard
}

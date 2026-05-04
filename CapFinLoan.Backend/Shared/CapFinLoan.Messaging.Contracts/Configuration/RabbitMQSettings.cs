namespace CapFinLoan.Messaging.Contracts.Configuration;

/// <summary>
/// Strongly-typed binding for the "RabbitMQ" section in appsettings.json.
/// </summary>
public sealed class RabbitMQSettings
{
    public const string SectionName = "RabbitMQ";

    /// <summary>Hostname or IP of the RabbitMQ broker (e.g. "localhost" or "rabbitmq").</summary>
    public string Host { get; init; } = "localhost";

    /// <summary>AMQP port. Default: 5672.</summary>
    public int Port { get; init; } = 5672;

    /// <summary>RabbitMQ virtual host. Default: "/".</summary>
    public string VirtualHost { get; init; } = "/";

    /// <summary>Login username.</summary>
    public string Username { get; init; } = "guest";

    /// <summary>Login password.</summary>
    public string Password { get; init; } = "guest";

    /// <summary>Retry policy applied at startup (waiting for broker to become available).</summary>
    public RetrySettings ConnectionRetry { get; init; } = new();

    /// <summary>Retry policy applied per publish attempt.</summary>
    public RetrySettings PublishRetry { get; init; } = new();

    /// <summary>Consumer reliability settings (ack/nack/DLQ behaviour).</summary>
    public ConsumerSettings Consumer { get; init; } = new();

    /// <summary>
    /// How long (seconds) RabbitMQ's built-in automatic recovery waits between
    /// reconnect attempts after an established connection drops.
    /// </summary>
    public int NetworkRecoveryIntervalSeconds { get; init; } = 10;
}

/// <summary>
/// Exponential-backoff retry policy: delay = InitialDelaySeconds * 2^attempt (capped at MaxDelaySeconds).
/// A random jitter of ±JitterSeconds is added to avoid thundering-herd on broker restart.
/// </summary>
public sealed class RetrySettings
{
    /// <summary>Maximum number of attempts before giving up.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Base delay in seconds for the first retry.</summary>
    public int InitialDelaySeconds { get; init; } = 2;

    /// <summary>Upper cap on the computed delay.</summary>
    public int MaxDelaySeconds { get; init; } = 30;

    /// <summary>Maximum random jitter added to each delay (seconds).</summary>
    public int JitterSeconds { get; init; } = 2;
}

/// <summary>
/// Controls ack/nack/DLQ behaviour for RabbitMqConsumer&lt;T&gt;.
/// </summary>
public sealed class ConsumerSettings
{
    /// <summary>
    /// Number of unacknowledged messages held by the consumer at once.
    /// 1 = sequential processing (safe default). Raise for higher throughput.
    /// </summary>
    public ushort PrefetchCount { get; init; } = 1;

    /// <summary>
    /// After this many x-death redelivery cycles the message is dead-lettered
    /// instead of requeued, preventing infinite retry loops.
    /// </summary>
    public int MaxDeliveryCount { get; init; } = 3;

    /// <summary>
    /// TTL (milliseconds) for messages on the main queue.
    /// Messages not consumed within this window are dead-lettered.
    /// 0 = no TTL (messages never expire). Default: 30 minutes.
    /// </summary>
    public int MessageTtlMs { get; init; } = 1_800_000; // 30 min

    /// <summary>
    /// TTL (milliseconds) for messages on the dead-letter queue.
    /// Prevents the DLQ from growing unbounded. Default: 7 days.
    /// </summary>
    public int DlqMessageTtlMs { get; init; } = 604_800_000; // 7 days

    /// <summary>
    /// Maximum seconds to wait for in-flight message handlers to complete
    /// during graceful shutdown before forcibly closing the channel.
    /// Default: 10 seconds.
    /// </summary>
    public int ShutdownTimeoutSeconds { get; init; } = 10;
}

using System.Text;
using System.Text.Json;
using CapFinLoan.Messaging.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CapFinLoan.Messaging.Contracts.Messaging;

/// <summary>
/// Publishes messages to a per-event-type fanout exchange.
/// Each consuming service binds its own durable queue to the exchange,
/// so every service receives every message independently (fan-out pattern).
///
/// Exchange naming: kebab-case of the event type name
///   ApplicationStatusChangedEvent → application-status-changed-event (exchange)
///
/// Queue naming (consumer side): {exchange}.{QueueSuffix}
///   application-status-changed-event.app-svc
///   application-status-changed-event.doc-svc
///   application-status-changed-event.notif-svc
/// </summary>
public sealed class RabbitMqPublisher
{
    private readonly RabbitMqConnectionFactory  _connectionFactory;
    private readonly RabbitMQSettings           _settings;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private static readonly Random Jitter = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false,
    };

    public RabbitMqPublisher(
        RabbitMqConnectionFactory  connectionFactory,
        IOptions<RabbitMQSettings> settings,
        ILogger<RabbitMqPublisher> logger)
    {
        _connectionFactory = connectionFactory;
        _settings          = settings.Value;
        _logger            = logger;
    }

    /// <summary>
    /// Publishes <paramref name="message"/> to the fanout exchange for event type <typeparamref name="T"/>.
    /// The exchange name is derived from the type name (kebab-case).
    /// </summary>
    public async Task PublishAsync<T>(
        string queueName,          // kept for backward-compat; used as exchange name
        T message,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var body = Encoding.UTF8.GetBytes(json);

        _logger.LogDebug(
            "Publishing {EventType} → exchange '{Exchange}' ({Bytes} bytes)",
            typeof(T).Name, queueName, body.Length);

        await PublishWithRetryAsync(queueName, typeof(T).Name, body, cancellationToken);
    }

    // ── Retry loop ────────────────────────────────────────────────────────────

    private async Task PublishWithRetryAsync(
        string exchangeName,
        string eventTypeName,
        byte[] body,
        CancellationToken ct)
    {
        var retry = _settings.PublishRetry;

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            try
            {
                await PublishOnceAsync(exchangeName, eventTypeName, body, ct);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (AlreadyClosedException ex) when (attempt < retry.MaxAttempts)
                { await DelayAndLog(ex, attempt, retry, exchangeName, ct); }
            catch (ChannelAllocationException ex) when (attempt < retry.MaxAttempts)
                { await DelayAndLog(ex, attempt, retry, exchangeName, ct); }
            catch (Exception ex) when (attempt < retry.MaxAttempts)
                { await DelayAndLog(ex, attempt, retry, exchangeName, ct); }
        }

        _logger.LogError(
            "All {Max} publish attempts failed for exchange '{Exchange}'. Giving up.",
            retry.MaxAttempts, exchangeName);

        await PublishOnceAsync(exchangeName, eventTypeName, body, ct);
    }

    private async Task PublishOnceAsync(
        string exchangeName,
        string eventTypeName,
        byte[] body,
        CancellationToken ct)
    {
        var connection = await _connectionFactory.GetConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // Declare the fanout exchange — idempotent, safe to call on every publish
        await channel.ExchangeDeclareAsync(
            exchange:   exchangeName,
            type:       ExchangeType.Fanout,
            durable:    true,
            autoDelete: false,
            cancellationToken: ct);

        var props = new BasicProperties
        {
            ContentType     = "application/json",
            ContentEncoding = "UTF-8",
            DeliveryMode    = DeliveryModes.Persistent,
            Type            = eventTypeName,
            MessageId       = Guid.NewGuid().ToString("N"),
            Timestamp       = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers         = new Dictionary<string, object?>
            {
                ["x-service-origin"] = Environment.MachineName,
                ["x-published-utc"]  = DateTimeOffset.UtcNow.ToString("O"),
            },
        };

        // Publish to the fanout exchange — routingKey is ignored by fanout
        await channel.BasicPublishAsync(
            exchange:        exchangeName,
            routingKey:      string.Empty,
            mandatory:       false,
            basicProperties: props,
            body:            body,
            cancellationToken: ct);

        _logger.LogInformation(
            "Published {EventType} → exchange '{Exchange}' | msgId={MessageId} | {Bytes} bytes",
            eventTypeName, exchangeName, props.MessageId, body.Length);
    }

    private async Task DelayAndLog(
        Exception ex, int attempt, RetrySettings retry,
        string exchange, CancellationToken ct)
    {
        var delay = ComputeDelay(attempt, retry);
        _logger.LogWarning(ex,
            "Publish to '{Exchange}' failed (attempt {Attempt}/{Max}). Retrying in {Delay:F1}s...",
            exchange, attempt, retry.MaxAttempts, delay.TotalSeconds);
        await Task.Delay(delay, ct);
    }

    private static TimeSpan ComputeDelay(int attempt, RetrySettings retry)
    {
        var exponential = retry.InitialDelaySeconds * Math.Pow(2, attempt - 1);
        var capped      = Math.Min(exponential, retry.MaxDelaySeconds);
        var jitter      = Jitter.NextDouble() * retry.JitterSeconds;
        return TimeSpan.FromSeconds(capped + jitter);
    }
}

using System.Text;
using System.Text.Json;
using CapFinLoan.Messaging.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CapFinLoan.Messaging.Contracts.Messaging;

/// <summary>
/// Production-grade RabbitMQ publisher using RabbitMQ.Client directly.
/// Durable queues, persistent messages, DLQ routing, publish retry with exponential backoff.
/// </summary>
public sealed class RabbitMqPublisher
{
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMQSettings          _settings;
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

    public async Task PublishAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var body = Encoding.UTF8.GetBytes(json);

        _logger.LogDebug(
            "Publishing {EventType} → queue '{Queue}' ({Bytes} bytes)",
            typeof(T).Name, queueName, body.Length);

        await PublishWithRetryAsync(queueName, typeof(T).Name, body, cancellationToken);
    }

    // -------------------------------------------------------------------------

    private async Task PublishWithRetryAsync(
        string queueName,
        string eventTypeName,
        byte[] body,
        CancellationToken ct)
    {
        var retry = _settings.PublishRetry;

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            try
            {
                await PublishOnceAsync(queueName, eventTypeName, body, ct);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (AlreadyClosedException ex) when (attempt < retry.MaxAttempts)
            {
                await DelayAndLog(ex, attempt, retry, queueName, ct);
            }
            catch (ChannelAllocationException ex) when (attempt < retry.MaxAttempts)
            {
                await DelayAndLog(ex, attempt, retry, queueName, ct);
            }
            catch (Exception ex) when (attempt < retry.MaxAttempts)
            {
                await DelayAndLog(ex, attempt, retry, queueName, ct);
            }
        }

        // Final attempt — let exception propagate
        _logger.LogError(
            "All {Max} publish attempts failed for queue '{Queue}'. Giving up.",
            retry.MaxAttempts, queueName);

        await PublishOnceAsync(queueName, eventTypeName, body, ct);
    }

    private async Task PublishOnceAsync(
        string queueName,
        string eventTypeName,
        byte[] body,
        CancellationToken ct)
    {
        var connection = await _connectionFactory.GetConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var dlxName = $"{queueName}.dlx";
        var dlqName = $"{queueName}.dlq";

        // Dead-letter exchange
        await channel.ExchangeDeclareAsync(
            exchange: dlxName, type: ExchangeType.Fanout,
            durable: true, autoDelete: false, cancellationToken: ct);

        // Dead-letter queue — declare with same TTL as consumer to avoid PRECONDITION_FAILED
        var dlqArgs = new Dictionary<string, object?> { ["x-message-ttl"] = 604_800_000 };
        await channel.QueueDeclareAsync(
            queue: dlqName, durable: true, exclusive: false,
            autoDelete: false, arguments: dlqArgs, cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: dlqName, exchange: dlxName,
            routingKey: string.Empty, cancellationToken: ct);

        // Main durable queue with DLX routing — declare with same TTL as consumer to avoid PRECONDITION_FAILED
        await channel.QueueDeclareAsync(
            queue:      queueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            arguments:  new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"]    = dlxName,
                ["x-dead-letter-routing-key"] = string.Empty,  // matches consumer declaration
                ["x-message-ttl"]             = 1_800_000,     // 30 min — matches ConsumerSettings.MessageTtlMs default
            },
            cancellationToken: ct);

        // Message properties — persistent, JSON, with tracing headers
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

        await channel.BasicPublishAsync(
            exchange:        string.Empty,
            routingKey:      queueName,
            mandatory:       false,
            basicProperties: props,
            body:            body,
            cancellationToken: ct);

        _logger.LogInformation(
            "Published {EventType} → queue '{Queue}' | msgId={MessageId} | {Bytes} bytes",
            eventTypeName, queueName, props.MessageId, body.Length);
    }

    private async Task DelayAndLog(
        Exception ex, int attempt, RetrySettings retry,
        string queue, CancellationToken ct)
    {
        var delay = ComputeDelay(attempt, retry);
        _logger.LogWarning(ex,
            "Publish to '{Queue}' failed (attempt {Attempt}/{Max}). Retrying in {Delay:F1}s...",
            queue, attempt, retry.MaxAttempts, delay.TotalSeconds);
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

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CapFinLoan.Messaging.Contracts.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CapFinLoan.Messaging.Contracts.Messaging;

/// <summary>
/// Generic BackgroundService that consumes messages of type <typeparamref name="T"/>
/// from a durable RabbitMQ queue using RabbitMQ.Client v7.
///
/// ── Graceful Shutdown sequence ───────────────────────────────────────────────
///
///  When the host signals shutdown (stoppingToken is cancelled):
///
///  1. BasicCancelAsync(consumerTag)
///     → tells the broker to stop delivering new messages to this consumer.
///     → in-flight messages already dispatched to Task.Run are unaffected.
///
///  2. Drain in-flight handlers
///     → wait up to Consumer.ShutdownTimeoutSeconds for all running Task.Run
///       handlers to complete (they nack+requeue on OperationCanceledException).
///     → if the timeout expires, log a warning and proceed — the broker will
///       redeliver unacked messages to another instance.
///
///  3. Channel.CloseAsync(200, "Consumer shutdown")
///     → sends AMQP channel.close with a normal close code and reason string.
///     → the broker acknowledges and releases server-side resources for this channel.
///
///  4. Connection.CloseAsync(timeout)  [in RabbitMqConnectionFactory.DisposeAsync]
///     → called by the DI container when the singleton is disposed at host shutdown.
///     → sends AMQP connection.close, waits for broker acknowledgement.
///     → bounded by a timeout so shutdown never hangs indefinitely.
///
/// ── Reliability model ────────────────────────────────────────────────────────
///
///  autoAck = false — broker holds the message until we explicitly ack or nack.
///
///  SUCCESS          → BasicAck   (message removed permanently)
///  TRANSIENT ERROR  → BasicNack(requeue: true)   (retry up to MaxDeliveryCount)
///  PERMANENT ERROR  → BasicNack(requeue: false)  (dead-letter immediately)
///  SHUTDOWN         → BasicNack(requeue: true)   (another instance picks it up)
///  MAX RETRIES      → BasicNack(requeue: false)  (dead-letter, stop retrying)
///
/// </summary>
public sealed partial class RabbitMqConsumer<T> : BackgroundService where T : class
{
    private readonly RabbitMqConnectionFactory    _connectionFactory;
    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ConsumerSettings             _consumer;
    private readonly string                       _queueSuffix;
    private readonly ILogger<RabbitMqConsumer<T>> _logger;

    private volatile int _inFlightCount;
    private readonly SemaphoreSlim _drainGate = new(0, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public RabbitMqConsumer(
        RabbitMqConnectionFactory    connectionFactory,
        IServiceScopeFactory         scopeFactory,
        IOptions<RabbitMQSettings>   settings,
        ILogger<RabbitMqConsumer<T>> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory      = scopeFactory;
        _consumer          = settings.Value.Consumer;
        _queueSuffix       = settings.Value.QueueSuffix ?? string.Empty;
        _logger            = logger;
    }

    // Dispose the SemaphoreSlim when the BackgroundService is disposed.
    // BackgroundService.Dispose() calls Dispose(true) — override to clean up.
    public override void Dispose()
    {
        _drainGate.Dispose();
        base.Dispose();
    }

    private string QueueName => string.IsNullOrEmpty(_queueSuffix)
        ? ToQueueName(typeof(T).Name)
        : $"{ToQueueName(typeof(T).Name)}.{_queueSuffix}";

    // =========================================================================
    // BackgroundService entry point
    // =========================================================================

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[{Consumer}] Starting — queue: '{Queue}' | prefetch={Prefetch} | " +
            "maxDelivery={Max} | shutdownTimeout={Timeout}s",
            typeof(T).Name, QueueName,
            _consumer.PrefetchCount, _consumer.MaxDeliveryCount,
            _consumer.ShutdownTimeoutSeconds);

        ValidateHandlerRegistration();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // clean shutdown — exit the loop
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[{Consumer}] Channel faulted. Reconnecting in 5s...",
                    typeof(T).Name);

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("[{Consumer}] Stopped.", typeof(T).Name);
    }

    // =========================================================================
    // Core consume loop — one channel lifetime
    // =========================================================================

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var connection = await _connectionFactory.GetConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await DeclareTopologyAsync(channel, ct);

        await channel.BasicQosAsync(
            prefetchSize:  0,
            prefetchCount: _consumer.PrefetchCount,
            global:        false,
            cancellationToken: ct);

        // Gate: keeps ConsumeAsync alive until cancellation or channel fault.
        using var gate = new SemaphoreSlim(0, 1);

        channel.ChannelShutdownAsync += (_, args) =>
        {
            _logger.LogWarning(
                "[{Consumer}] Channel shutdown: {Reason}. Will reconnect.",
                typeof(T).Name, args.ReplyText);
            gate.Release();
            return Task.CompletedTask;
        };

        // Register cancellation → release gate so ConsumeAsync can proceed to shutdown
        await using var ctReg = ct.Register(() => gate.Release());

        var consumer = new SyncDispatchConsumer(channel, ea =>
        {
            // Capture channel in a local — the lambda outlives ConsumeAsync if
            // Task.Run is still running when the channel is disposed on shutdown.
            // The channel reference itself is safe to hold; SafeAckAsync /
            // SafeNackAsync handle AlreadyClosedException gracefully.
            var capturedChannel = channel;

            Interlocked.Increment(ref _inFlightCount);

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleDeliveryAsync(capturedChannel, ea, ct);
                }
                finally
                {
                    if (Interlocked.Decrement(ref _inFlightCount) == 0 && ct.IsCancellationRequested)
                        TryReleaseDrainGate();
                }
            }, CancellationToken.None);
        });

        var consumerTag = await channel.BasicConsumeAsync(
            queue:    QueueName,
            autoAck:  false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation(
            "[{Consumer}] Listening on queue '{Queue}' (autoAck=false, prefetch={Prefetch}, tag={Tag})",
            typeof(T).Name, QueueName, _consumer.PrefetchCount, consumerTag);

        // Block until cancellation or channel fault
        await gate.WaitAsync(CancellationToken.None);

        // ── Graceful shutdown sequence ────────────────────────────────────────
        if (ct.IsCancellationRequested)
        {
            await ShutdownConsumerAsync(channel, consumerTag);
        }
    }

    // =========================================================================
    // Graceful shutdown
    // =========================================================================

    private async Task ShutdownConsumerAsync(IChannel channel, string consumerTag)
    {
        var timeout = TimeSpan.FromSeconds(_consumer.ShutdownTimeoutSeconds);

        // Step 1: Cancel the consumer — stop the broker sending new deliveries
        _logger.LogInformation(
            "[{Consumer}] Shutdown: cancelling consumer tag '{Tag}'...",
            typeof(T).Name, consumerTag);

        try
        {
            using var cancelCts = new CancellationTokenSource(timeout);
            await channel.BasicCancelAsync(consumerTag, cancellationToken: cancelCts.Token);

            _logger.LogInformation(
                "[{Consumer}] Consumer tag '{Tag}' cancelled.",
                typeof(T).Name, consumerTag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{Consumer}] BasicCancelAsync failed for tag '{Tag}'. Proceeding with shutdown.",
                typeof(T).Name, consumerTag);
        }

        // Step 2: Drain in-flight handlers
        var currentInFlight = Volatile.Read(ref _inFlightCount);
        if (currentInFlight > 0)
        {
            _logger.LogInformation(
                "[{Consumer}] Draining {Count} in-flight handler(s) (timeout={Timeout}s)...",
                typeof(T).Name, currentInFlight, _consumer.ShutdownTimeoutSeconds);

            var drained = await _drainGate.WaitAsync(timeout);

            if (drained)
            {
                _logger.LogInformation(
                    "[{Consumer}] All in-flight handlers completed.",
                    typeof(T).Name);
            }
            else
            {
                _logger.LogWarning(
                    "[{Consumer}] Drain timeout ({Timeout}s) expired with {Count} handler(s) still running. " +
                    "Unacked messages will be redelivered by the broker.",
                    typeof(T).Name, _consumer.ShutdownTimeoutSeconds,
                    Volatile.Read(ref _inFlightCount));
            }
        }

        // Step 3: Close the channel with a normal AMQP close code
        _logger.LogInformation(
            "[{Consumer}] Closing channel...",
            typeof(T).Name);

        try
        {
            using var closeCts = new CancellationTokenSource(timeout);
            await channel.CloseAsync(closeCts.Token);

            _logger.LogInformation(
                "[{Consumer}] Channel closed cleanly.",
                typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{Consumer}] Channel close failed. The connection factory will clean up on dispose.",
                typeof(T).Name);
        }
    }

    private void TryReleaseDrainGate()
    {
        // SemaphoreSlim.Release throws SemaphoreFullException if the count
        // would exceed the max (1). Catch it rather than checking CurrentCount
        // first — the check-then-act pattern has a TOCTOU race when multiple
        // Task.Run threads decrement to zero simultaneously.
        try { _drainGate.Release(); }
        catch (SemaphoreFullException) { /* already released by a concurrent thread */ }
    }

    // =========================================================================
    // Topology declaration
    // =========================================================================

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
    {
        // The exchange name is the base event name (no suffix) — shared by all services
        var exchangeName = ToQueueName(typeof(T).Name);
        var queueName    = QueueName;   // suffixed: e.g. application-status-changed-event.app-svc
        var dlxName      = $"{queueName}.dlx";
        var dlqName      = $"{queueName}.dlq";

        // ── Fanout exchange ───────────────────────────────────────────────────
        // Declared by both publisher and consumer (idempotent).
        // All services bind their own queue to this exchange.
        await channel.ExchangeDeclareAsync(
            exchange:   exchangeName,
            type:       ExchangeType.Fanout,
            durable:    true,
            autoDelete: false,
            cancellationToken: ct);

        // ── Dead-letter exchange ──────────────────────────────────────────────
        await channel.ExchangeDeclareAsync(
            exchange:   dlxName,
            type:       ExchangeType.Fanout,
            durable:    true,
            autoDelete: false,
            cancellationToken: ct);

        // ── Dead-letter queue ─────────────────────────────────────────────────
        var dlqArgs = new Dictionary<string, object?>();
        if (_consumer.DlqMessageTtlMs > 0)
            dlqArgs["x-message-ttl"] = _consumer.DlqMessageTtlMs;

        await channel.QueueDeclareAsync(
            queue:      dlqName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            arguments:  dlqArgs.Count > 0 ? dlqArgs : null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue:      dlqName,
            exchange:   dlxName,
            routingKey: string.Empty,
            cancellationToken: ct);

        // ── Main queue ────────────────────────────────────────────────────────
        var queueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"]     = dlxName,
            ["x-dead-letter-routing-key"]  = string.Empty,
        };
        if (_consumer.MessageTtlMs > 0)
            queueArgs["x-message-ttl"] = _consumer.MessageTtlMs;

        await channel.QueueDeclareAsync(
            queue:      queueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            arguments:  queueArgs,
            cancellationToken: ct);

        // ── Bind this service's queue to the fanout exchange ──────────────────
        // routingKey is ignored by fanout — every bound queue gets every message
        await channel.QueueBindAsync(
            queue:      queueName,
            exchange:   exchangeName,
            routingKey: string.Empty,
            cancellationToken: ct);

        _logger.LogInformation(
            "[{Consumer}] Topology declared — " +
            "exchange: '{Exchange}' → queue: '{Queue}' (ttl={TtlMs}ms) → dlx: '{Dlx}' → dlq: '{Dlq}' (ttl={DlqTtlMs}ms)",
            typeof(T).Name,
            exchangeName,
            queueName,  _consumer.MessageTtlMs,
            dlxName,
            dlqName,    _consumer.DlqMessageTtlMs);
    }

    // =========================================================================
    // Per-message processing
    // =========================================================================

    // Header name written by this consumer to track retry attempts.
    // Using a custom header (not x-death) gives us precise control:
    //   - x-death only increments on DLQ cycles, not on nack+requeue
    //   - x-retry-count increments on every transient failure requeue
    private const string RetryCountHeader = "x-retry-count";

    private async Task HandleDeliveryAsync(
        IChannel              channel,
        BasicDeliverEventArgs ea,
        CancellationToken     ct)
    {
        var deliveryTag  = ea.DeliveryTag;
        var retryCount   = GetRetryCount(ea);          // reads x-retry-count header
        var eventType    = typeof(T).Name;
        var queue        = QueueName;

        // ── Log: message received ─────────────────────────────────────────────
        Log.MessageReceived(_logger, eventType, queue, deliveryTag,
            ea.Redelivered, retryCount, _consumer.MaxDeliveryCount);

        // ── Max retries guard — uses x-retry-count header ─────────────────────
        // MaxDeliveryCount = 3 means: attempt 1 (original) + 3 retries = 4 total.
        if (retryCount >= _consumer.MaxDeliveryCount)
        {
            Log.MaxRetriesExceeded(_logger, eventType, deliveryTag, retryCount, _consumer.MaxDeliveryCount);
            await SafeNackAsync(channel, deliveryTag, requeue: false);
            return;
        }

        T? message = DeserializeMessage(ea.Body.Span, eventType, deliveryTag);
        if (message is null)
        {
            await SafeNackAsync(channel, deliveryTag, requeue: false);
            return;
        }

        await DispatchInScopeAsync(channel, ea, deliveryTag, retryCount, message, ct);
    }

    /// <summary>
    /// Deserializes the raw UTF-8 JSON bytes into <typeparamref name="T"/>.
    ///
    /// Returns null and logs an error on any deserialization failure so the
    /// caller can dead-letter the message without retrying.
    ///
    /// Property mapping (camelCase JSON → PascalCase C#):
    ///   documentId    → DocumentId
    ///   applicationId → ApplicationId
    ///   userId        → UserId
    ///   documentType  → DocumentType
    ///   fileName      → FileName
    ///   contentType   → ContentType
    ///   fileSizeBytes → FileSizeBytes
    ///   uploadedAtUtc → UploadedAtUtc
    /// </summary>
    private T? DeserializeMessage(ReadOnlySpan<byte> body, string eventType, ulong deliveryTag)
    {
        // Decode once — reused for both deserialization and error logging.
        // Avoids allocating the string twice on the failure path.
        var json = Encoding.UTF8.GetString(body);

        try
        {
            var message = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (message is null)
            {
                _logger.LogError(
                    "[{EventType}] Deserialized null for delivery {DeliveryTag}. " +
                    "Raw JSON: {Json}. Dead-lettering.",
                    eventType, deliveryTag, json);
                return null;
            }

            _logger.LogDebug(
                "[{EventType}] Deserialized delivery {DeliveryTag} → {Type}.",
                eventType, deliveryTag, typeof(T).Name);

            return message;
        }
        catch (JsonException ex)
        {
            Log.DeserializationFailed(_logger, ex, eventType, deliveryTag);
            _logger.LogDebug(
                "[{EventType}] Raw payload for failed delivery {DeliveryTag}: {Json}",
                eventType, deliveryTag, json);
            return null;
        }
        catch (Exception ex) when (IsPermanentFailure(ex))
        {
            Log.DeserializationFailed(_logger, ex, eventType, deliveryTag);
            return null;
        }
    }

    private async Task DispatchInScopeAsync(
        IChannel              channel,
        BasicDeliverEventArgs ea,
        ulong                 deliveryTag,
        int                   retryCount,
        T                     message,
        CancellationToken     ct)
    {
        var eventType = typeof(T).Name;
        var sw        = Stopwatch.StartNew();

        await using var scope = _scopeFactory.CreateAsyncScope();

        try
        {
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>();

            Log.HandlerStarted(_logger, eventType, deliveryTag, handler.GetType().Name);

            var ack = await handler.HandleAsync(message, ct);

            sw.Stop();

            switch (ack.Decision)
            {
                case AckDecision.Ack:
                    await SafeAckAsync(channel, deliveryTag);
                    Log.MessageProcessed(_logger, eventType, deliveryTag, sw.ElapsedMilliseconds);
                    break;

                case AckDecision.NackRequeue:
                    Log.RetryAttempt(_logger, eventType, deliveryTag,
                        ea.BasicProperties.MessageId ?? "(no-id)",
                        retryCount + 1, _consumer.MaxDeliveryCount, ack.Reason);
                    await SafeRetryPublishAsync(channel, ea, retryCount + 1, deliveryTag, ct);
                    break;

                case AckDecision.NackDiscard:
                    Log.FinalFailure(_logger, eventType, deliveryTag,
                        ea.BasicProperties.MessageId ?? "(no-id)",
                        sw.ElapsedMilliseconds, ack.Reason);
                    await SafeNackAsync(channel, deliveryTag, requeue: false);
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            Log.ShutdownNack(_logger, eventType, deliveryTag);
            // On shutdown: nack+requeue without incrementing retry count —
            // the message wasn't actually processed, so the retry budget is preserved.
            await SafeNackAsync(channel, deliveryTag, requeue: true);
        }
        catch (Exception ex) when (IsPermanentFailure(ex))
        {
            sw.Stop();
            Log.PermanentFailure(_logger, ex, eventType, deliveryTag,
                ex.GetType().Name, ex.Message, sw.ElapsedMilliseconds);
            await SafeNackAsync(channel, deliveryTag, requeue: false);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.TransientFailure(_logger, ex, eventType, deliveryTag,
                ex.GetType().Name, ex.Message, sw.ElapsedMilliseconds);
            // Unexpected exception — republish with incremented retry count
            await SafeRetryPublishAsync(channel, ea, retryCount + 1, deliveryTag, ct);
        }
    }

    /// <summary>
    /// Republishes the original message with x-retry-count incremented, then acks the original.
    ///
    /// If the republish fails (e.g. channel closed mid-retry), falls back to
    /// SafeNackAsync(requeue: false) → dead-letter. This prevents the message
    /// from staying unacked indefinitely and avoids an infinite retry loop.
    ///
    /// Why republish instead of nack+requeue?
    ///   AMQP BasicNack cannot modify headers — the broker requeues the exact
    ///   original bytes. To track retry count in a header we must publish a new
    ///   message and ack the original.
    /// </summary>
    private async Task SafeRetryPublishAsync(
        IChannel              channel,
        BasicDeliverEventArgs ea,
        int                   nextRetryCount,
        ulong                 deliveryTag,
        CancellationToken     ct)
    {
        try
        {
            await RetryPublishAsync(channel, ea, nextRetryCount, ct);
            await SafeAckAsync(channel, deliveryTag); // ack original after successful republish
        }
        catch (Exception ex)
        {
            // Republish failed — fall back to dead-letter to avoid leaving the
            // message unacked (which would block the prefetch slot indefinitely).
            _logger.LogError(ex,
                "[{Consumer}] RetryPublish failed for delivery {Tag} (retry {Count}). " +
                "Dead-lettering to prevent infinite loop.",
                typeof(T).Name, deliveryTag, nextRetryCount);
            await SafeNackAsync(channel, deliveryTag, requeue: false);
        }
    }
    private async Task RetryPublishAsync(
        IChannel              channel,
        BasicDeliverEventArgs ea,
        int                   nextRetryCount,
        CancellationToken     ct)
    {
        // Copy all existing headers, then set/overwrite x-retry-count
        var headers = new Dictionary<string, object?>(
            ea.BasicProperties.Headers ?? new Dictionary<string, object?>());

        headers[RetryCountHeader] = nextRetryCount;

        var props = new BasicProperties
        {
            ContentType     = ea.BasicProperties.ContentType,
            ContentEncoding = ea.BasicProperties.ContentEncoding,
            DeliveryMode    = ea.BasicProperties.DeliveryMode,   // preserve persistence
            Type            = ea.BasicProperties.Type,
            MessageId       = ea.BasicProperties.MessageId,      // preserve original ID
            Timestamp       = ea.BasicProperties.Timestamp,
            Headers         = headers,
        };

        await channel.BasicPublishAsync(
            exchange:        string.Empty,  // default exchange — routes by queue name
            routingKey:      QueueName,
            mandatory:       false,
            basicProperties: props,
            body:            ea.Body,       // original body bytes — already copied in SyncDispatchConsumer
            cancellationToken: ct);

        _logger.LogDebug(
            "[{Consumer}] Republished delivery {Tag} with {Header}={Count}.",
            typeof(T).Name, ea.DeliveryTag, RetryCountHeader, nextRetryCount);
    }

    // =========================================================================
    // Safe ack/nack helpers
    // =========================================================================

    private async Task SafeAckAsync(IChannel channel, ulong deliveryTag)
    {
        try
        {
            await channel.BasicAckAsync(
                deliveryTag: deliveryTag,
                multiple:    false,
                cancellationToken: CancellationToken.None);
        }
        catch (AlreadyClosedException ex)
        {
            Log.AckFailed(_logger, ex, typeof(T).Name, deliveryTag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{Consumer}] Unexpected error acking delivery {Tag}.",
                typeof(T).Name, deliveryTag);
        }
    }

    private async Task SafeNackAsync(IChannel channel, ulong deliveryTag, bool requeue)
    {
        try
        {
            await channel.BasicNackAsync(
                deliveryTag: deliveryTag,
                multiple:    false,
                requeue:     requeue,
                cancellationToken: CancellationToken.None);
        }
        catch (AlreadyClosedException ex)
        {
            Log.NackFailed(_logger, ex, typeof(T).Name, deliveryTag, requeue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{Consumer}] Unexpected error nacking delivery {Tag}.",
                typeof(T).Name, deliveryTag);
        }
    }

    // =========================================================================
    // Exception classification
    // =========================================================================

    private static bool IsPermanentFailure(Exception ex) => ex is
        JsonException             or
        InvalidOperationException or
        ArgumentException         or
        NotSupportedException;

    // =========================================================================
    // Startup validation
    // =========================================================================

    private void ValidateHandlerRegistration()
    {
        try
        {
            using var scope   = _scopeFactory.CreateScope();
            var       handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>();

            _logger.LogDebug(
                "[{Consumer}] Handler validated: {HandlerType}.",
                typeof(T).Name, handler.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "[{Consumer}] IMessageHandler<{EventType}> is not registered. " +
                "Add services.AddScoped<IMessageHandler<{EventType}>, YourHandler>() " +
                "in DependencyInjection.cs.",
                typeof(T).Name, typeof(T).Name, typeof(T).Name);
            throw;
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Reads the x-retry-count header written by RetryPublishAsync.
    /// Returns 0 on first delivery (header absent — message has never been retried).
    ///
    /// Header lifecycle:
    ///   First delivery:  header absent → returns 0
    ///   After 1st retry: x-retry-count = 1
    ///   After 2nd retry: x-retry-count = 2
    ///   After 3rd retry: x-retry-count = 3 → MaxDeliveryCount reached → dead-letter
    /// </summary>
    private static int GetRetryCount(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is null ||
            !ea.BasicProperties.Headers.TryGetValue(RetryCountHeader, out var raw))
        {
            return 0; // first delivery — no retries yet
        }

        return raw switch
        {
            int    i => i,
            long   l => (int)l,   // RabbitMQ encodes integers as long in AMQP headers
            byte[] b => b.Length >= 4 ? BitConverter.ToInt32(b, 0) : 0,
            _        => 0
        };
    }

    private static string ToQueueName(string typeName) =>
        string.Concat(typeName.Select((c, i) =>
            i > 0 && char.IsUpper(c)
                ? $"-{char.ToLowerInvariant(c)}"
                : char.ToLowerInvariant(c).ToString()));

    // =========================================================================
    // LoggerMessage source-generated delegates
    // =========================================================================
    //
    // Using [LoggerMessage] avoids boxing of value types and string allocation
    // on every log call — important for high-frequency paths like message receipt.
    // The compiler generates a static cached delegate per method at build time.

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug,
            Message = "[{EventType}] Received delivery {DeliveryTag} on '{Queue}' | " +
                      "redelivered={Redelivered} | attempt={Count}/{Max}")]
        public static partial void MessageReceived(ILogger logger,
            string eventType, string queue, ulong deliveryTag,
            bool redelivered, int count, int max);

        [LoggerMessage(Level = LogLevel.Debug,
            Message = "[{EventType}] Handler started: {HandlerType} | delivery={DeliveryTag}")]
        public static partial void HandlerStarted(ILogger logger,
            string eventType, ulong deliveryTag, string handlerType);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "[{EventType}] SUCCESS — delivery {DeliveryTag} processed and acked in {ElapsedMs}ms")]
        public static partial void MessageProcessed(ILogger logger,
            string eventType, ulong deliveryTag, long elapsedMs);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "[{EventType}] SHUTDOWN — nacking delivery {DeliveryTag} with requeue")]
        public static partial void ShutdownNack(ILogger logger,
            string eventType, ulong deliveryTag);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "[{EventType}] RETRY — delivery {DeliveryTag} | msgId={MessageId} | " +
                      "attempt {RetryCount}/{Max} | reason: {Reason}")]
        public static partial void RetryAttempt(ILogger logger,
            string eventType, ulong deliveryTag, string messageId,
            int retryCount, int max, string? reason);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "[{EventType}] FINAL FAILURE — delivery {DeliveryTag} | msgId={MessageId} | " +
                      "dead-lettering after {ElapsedMs}ms | reason: {Reason}")]
        public static partial void FinalFailure(ILogger logger,
            string eventType, ulong deliveryTag, string messageId,
            long elapsedMs, string? reason);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "[{EventType}] MAX RETRIES — delivery {DeliveryTag} reached {RetryCount}/{Max} retries. Dead-lettering.")]
        public static partial void MaxRetriesExceeded(ILogger logger,
            string eventType, ulong deliveryTag, int retryCount, int max);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "[{EventType}] DESERIALIZATION FAILED — delivery {DeliveryTag}. Dead-lettering.")]
        public static partial void DeserializationFailed(ILogger logger, Exception ex,
            string eventType, ulong deliveryTag);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "[{EventType}] PERMANENT FAILURE — dead-lettering delivery {DeliveryTag} " +
                      "after {ElapsedMs}ms | {ExceptionType}: {ExceptionMessage}")]
        public static partial void PermanentFailure(ILogger logger, Exception ex,
            string eventType, ulong deliveryTag,
            string exceptionType, string exceptionMessage, long elapsedMs);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "[{EventType}] TRANSIENT FAILURE — nacking delivery {DeliveryTag} with requeue " +
                      "after {ElapsedMs}ms | {ExceptionType}: {ExceptionMessage}")]
        public static partial void TransientFailure(ILogger logger, Exception ex,
            string eventType, ulong deliveryTag,
            string exceptionType, string exceptionMessage, long elapsedMs);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "[{EventType}] ACK FAILED — delivery {DeliveryTag}: channel already closed. " +
                      "Message will be redelivered.")]
        public static partial void AckFailed(ILogger logger, Exception ex,
            string eventType, ulong deliveryTag);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "[{EventType}] NACK FAILED — delivery {DeliveryTag} (requeue={Requeue}): channel already closed.")]
        public static partial void NackFailed(ILogger logger, Exception ex,
            string eventType, ulong deliveryTag, bool requeue);
    }

    // =========================================================================
    // SyncDispatchConsumer — v7 equivalent of EventingBasicConsumer
    // =========================================================================

    private sealed class SyncDispatchConsumer : AsyncDefaultBasicConsumer
    {
        private readonly Action<BasicDeliverEventArgs> _onReceived;

        public SyncDispatchConsumer(IChannel channel, Action<BasicDeliverEventArgs> onReceived)
            : base(channel)
        {
            _onReceived = onReceived;
        }

        public override Task HandleBasicDeliverAsync(
            string               consumerTag,
            ulong                deliveryTag,
            bool                 redelivered,
            string               exchange,
            string               routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken    cancellationToken = default)
        {
            var bodyCopy = body.ToArray();

            var ea = new BasicDeliverEventArgs(
                consumerTag: consumerTag,
                deliveryTag: deliveryTag,
                redelivered: redelivered,
                exchange:    exchange,
                routingKey:  routingKey,
                properties:  properties,
                body:        bodyCopy);

            _onReceived(ea);
            return Task.CompletedTask;
        }
    }
}

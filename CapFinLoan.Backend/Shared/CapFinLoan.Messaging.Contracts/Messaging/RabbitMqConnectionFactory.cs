using CapFinLoan.Messaging.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CapFinLoan.Messaging.Contracts.Messaging;

/// <summary>
/// Manages a single long-lived AMQP connection to RabbitMQ.
///
/// Durability guarantees:
///   - Startup retry: if the broker is not yet ready (e.g. Docker container still booting),
///     the factory retries with exponential backoff + jitter up to ConnectionRetry.MaxAttempts.
///   - Automatic recovery: once connected, RabbitMQ.Client's built-in topology recovery
///     transparently reconnects and re-declares queues/exchanges after transient drops.
///   - Thread safety: double-checked locking via SemaphoreSlim ensures only one connection
///     is ever created even under concurrent first-call pressure.
///
/// Registered as Singleton — one connection per process lifetime.
/// </summary>
public sealed class RabbitMqConnectionFactory : IAsyncDisposable
{
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<RabbitMqConnectionFactory> _logger;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly Random Jitter = new();
    private volatile bool _disposed;

    public RabbitMqConnectionFactory(
        IOptions<RabbitMQSettings> settings,
        ILogger<RabbitMqConnectionFactory> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_connection is { IsOpen: true })
                return _connection;

            _connection = await CreateConnectionWithRetryAsync(ct);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IConnection> CreateConnectionWithRetryAsync(CancellationToken ct)
    {
        var retry   = _settings.ConnectionRetry;
        var factory = BuildConnectionFactory();

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "RabbitMQ: connecting to {Host}:{Port}{VHost} (attempt {Attempt}/{Max})",
                    _settings.Host, _settings.Port, _settings.VirtualHost, attempt, retry.MaxAttempts);

                var connection = await factory.CreateConnectionAsync(ct);

                _logger.LogInformation("RabbitMQ: connection established.");
                return connection;
            }
            catch (BrokerUnreachableException ex) when (attempt < retry.MaxAttempts)
            {
                var delay = ComputeDelay(attempt, retry);
                _logger.LogWarning(
                    ex,
                    "RabbitMQ: broker unreachable (attempt {Attempt}/{Max}). Retrying in {Delay:F1}s...",
                    attempt, retry.MaxAttempts, delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
            catch (Exception ex) when (attempt < retry.MaxAttempts)
            {
                var delay = ComputeDelay(attempt, retry);
                _logger.LogWarning(
                    ex,
                    "RabbitMQ: unexpected error on connect (attempt {Attempt}/{Max}). Retrying in {Delay:F1}s...",
                    attempt, retry.MaxAttempts, delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
        }

        // Final attempt — let the exception propagate so the host startup fails loudly
        _logger.LogError(
            "RabbitMQ: all {Max} connection attempts failed. Giving up.",
            retry.MaxAttempts);

        return await factory.CreateConnectionAsync(ct);
    }

    private ConnectionFactory BuildConnectionFactory() => new()
    {
        HostName    = _settings.Host,
        Port        = _settings.Port,
        VirtualHost = _settings.VirtualHost,
        UserName    = _settings.Username,
        Password    = _settings.Password,

        // Built-in recovery handles drops AFTER the initial connection is established.
        // It re-opens channels and re-declares topology automatically.
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled  = true,
        NetworkRecoveryInterval  = TimeSpan.FromSeconds(_settings.NetworkRecoveryIntervalSeconds),

        // Heartbeat detects silent TCP drops (e.g. NAT timeout, Docker network blip)
        RequestedHeartbeat = TimeSpan.FromSeconds(60),

        // How long to wait for the broker to accept the connection
        ContinuationTimeout = TimeSpan.FromSeconds(20),
    };

    /// <summary>
    /// Exponential backoff: delay = InitialDelay * 2^(attempt-1), capped at MaxDelay, plus random jitter.
    /// </summary>
    private static TimeSpan ComputeDelay(int attempt, RetrySettings retry)
    {
        var exponential = retry.InitialDelaySeconds * Math.Pow(2, attempt - 1);
        var capped      = Math.Min(exponential, retry.MaxDelaySeconds);
        var jitter      = Jitter.NextDouble() * retry.JitterSeconds;
        return TimeSpan.FromSeconds(capped + jitter);
    }

    public async ValueTask DisposeAsync()
    {
        // Set disposed flag inside the lock so any concurrent GetConnectionAsync
        // that is waiting on _lock.WaitAsync will see _disposed = true and throw
        // ObjectDisposedException rather than trying to create a new connection.
        await _lock.WaitAsync();
        try
        {
            _disposed = true;
        }
        finally
        {
            _lock.Release();
        }

        if (_connection is not null)
        {
            try
            {
                _logger.LogInformation("RabbitMQ: closing connection...");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _connection.CloseAsync(cts.Token);

                _logger.LogInformation("RabbitMQ: connection closed cleanly.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("RabbitMQ: connection close timed out. Forcing dispose.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ: error closing connection during shutdown.");
            }
            finally
            {
                _connection.Dispose();
                _connection = null;
            }
        }

        _lock.Dispose();
    }
}

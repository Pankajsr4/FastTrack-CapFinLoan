using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CapFinLoan.Api.Shared;

/// <summary>
/// Health check that verifies RabbitMQ is reachable by opening a short-lived
/// connection and immediately closing it.
///
/// Uses RabbitMQSettings from the service's appsettings.json — no extra config needed.
/// Timeout: 5 seconds (configurable via HealthCheckOptions when registering).
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqHealthCheckOptions _options;

    public RabbitMqHealthCheck(IOptions<RabbitMqHealthCheckOptions> options)
    {
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName    = _options.Host,
                Port        = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName    = _options.Username,
                Password    = _options.Password,
                // Short timeout — health checks must be fast
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                ContinuationTimeout        = TimeSpan.FromSeconds(5),
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);

            return connection.IsOpen
                ? HealthCheckResult.Healthy($"RabbitMQ is reachable at {_options.Host}:{_options.Port}")
                : HealthCheckResult.Unhealthy("RabbitMQ connection opened but is not in Open state.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"RabbitMQ is unreachable at {_options.Host}:{_options.Port}",
                exception: ex);
        }
    }
}

/// <summary>Options bound from the RabbitMQ section of appsettings.json.</summary>
public sealed class RabbitMqHealthCheckOptions
{
    public string Host        { get; init; } = "localhost";
    public int    Port        { get; init; } = 5672;
    public string VirtualHost { get; init; } = "/";
    public string Username    { get; init; } = "guest";
    public string Password    { get; init; } = "guest";
}

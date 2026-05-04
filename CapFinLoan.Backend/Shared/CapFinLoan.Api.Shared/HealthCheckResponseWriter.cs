using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CapFinLoan.Api.Shared;

/// <summary>
/// Writes health check results as a structured JSON response.
///
/// Response shape:
/// {
///   "status": "Healthy",
///   "duration": "00:00:00.123",
///   "checks": [
///     { "name": "database", "status": "Healthy",   "description": "...", "duration": "00:00:00.050" },
///     { "name": "rabbitmq", "status": "Unhealthy",  "description": "...", "duration": "00:00:05.001" }
///   ]
/// }
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = true,
    };

    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            status   = report.Status.ToString(),
            duration = report.TotalDuration.ToString(),
            checks   = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description ?? e.Value.Exception?.Message,
                duration    = e.Value.Duration.ToString(),
                tags        = e.Value.Tags,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(result, JsonOptions));
    }
}

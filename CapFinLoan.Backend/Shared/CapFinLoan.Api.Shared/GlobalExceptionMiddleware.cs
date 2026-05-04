using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Api.Shared;

/// <summary>
/// Catches all unhandled exceptions and returns a standardised ApiResponse.
///
/// Exception → HTTP status mapping:
///   KeyNotFoundException          → 404 Not Found
///   UnauthorizedAccessException   → 403 Forbidden
///   ArgumentException             → 400 Bad Request
///   InvalidOperationException     → 400 Bad Request
///   NotSupportedException         → 400 Bad Request
///   OperationCanceledException    → 499 Client Closed Request (logged at Debug)
///   Everything else               → 500 Internal Server Error (logged at Error)
///
/// In Development the exception message is included in the response.
/// In Production a generic message is returned to avoid leaking internals.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly bool _isDevelopment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next          = next;
        _logger        = logger;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = Classify(ex);

        // Log at appropriate level
        if (ex is OperationCanceledException)
            _logger.LogDebug(ex, "Request cancelled — {Path}", context.Request.Path);
        else if (statusCode >= 500)
            _logger.LogError(ex, "Unhandled exception — {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning(ex, "Client error {StatusCode} — {Method} {Path}", statusCode, context.Request.Method, context.Request.Path);

        // Build response
        var errorMessage = _isDevelopment ? ex.Message : message;
        var response     = ApiResponse.Fail(errorMessage);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static (int statusCode, string message) Classify(Exception ex) => ex switch
    {
        KeyNotFoundException          => (StatusCodes.Status404NotFound,            "The requested resource was not found."),
        UnauthorizedAccessException   => (StatusCodes.Status403Forbidden,           "You do not have permission to perform this action."),
        ArgumentException             => (StatusCodes.Status400BadRequest,          "Invalid request data."),
        InvalidOperationException     => (StatusCodes.Status400BadRequest,          "The operation could not be completed."),
        NotSupportedException         => (StatusCodes.Status400BadRequest,          "This operation is not supported."),
        OperationCanceledException    => (499,                                       "Request was cancelled."),
        _                             => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later."),
    };
}

// ── Extension method for clean registration ───────────────────────────────────
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}

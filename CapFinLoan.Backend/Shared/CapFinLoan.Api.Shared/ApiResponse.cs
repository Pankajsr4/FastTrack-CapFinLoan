namespace CapFinLoan.Api.Shared;

/// <summary>
/// Standardised API response envelope used by all services.
///
/// Success:  { "success": true,  "data": {...},  "errors": null }
/// Failure:  { "success": false, "data": null,   "errors": ["..."] }
/// </summary>
public sealed class ApiResponse<T>
{
    public bool     Success { get; init; }
    public T?       Data    { get; init; }
    public string[] Errors  { get; init; } = [];

    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static ApiResponse<T> Fail(params string[] errors) =>
        new() { Success = false, Errors = errors };
}

/// <summary>Non-generic version for responses with no data payload.</summary>
public sealed class ApiResponse
{
    public bool     Success { get; init; }
    public string[] Errors  { get; init; } = [];

    public static ApiResponse Ok()                    => new() { Success = true };
    public static ApiResponse Fail(params string[] errors) => new() { Success = false, Errors = errors };
}

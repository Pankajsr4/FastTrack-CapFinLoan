using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CapFinLoan.Admin.Application.Contracts.Responses;
using CapFinLoan.Admin.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Admin.Infrastructure.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly HttpClient _authClient;
    private readonly ILogger<AdminUserService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AdminUserService(
        IHttpClientFactory httpClientFactory,
        ILogger<AdminUserService> logger)
    {
        _authClient = httpClientFactory.CreateClient("AuthServiceClient");
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<UserSummaryResponse>> GetUsersAsync(
        string bearerToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/internal/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await _authClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var users = JsonSerializer.Deserialize<List<UserSummaryResponse>>(json, _jsonOptions)
                    ?? [];

        _logger.LogInformation("[AdminUserService] Fetched {Count} users from AuthService.", users.Count);
        return users;
    }

    public async Task<UserSummaryResponse> UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        string bearerToken,
        CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { isActive }, _jsonOptions);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"api/internal/users/{userId}/status");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _authClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"User {userId} not found.");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var user = JsonSerializer.Deserialize<UserSummaryResponse>(json, _jsonOptions)
                   ?? throw new InvalidOperationException("AuthService returned an empty user response.");

        _logger.LogInformation(
            "[AdminUserService] User {UserId} status set to IsActive={IsActive}.",
            userId, isActive);

        return user;
    }
}

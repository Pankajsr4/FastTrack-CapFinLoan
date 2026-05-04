using CapFinLoan.Auth.Application.Contracts.Requests;
using CapFinLoan.Auth.Application.Contracts.Responses;

namespace CapFinLoan.Auth.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> SignupAdminAsync(SignupRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserSummaryResponse> UpdateUserStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
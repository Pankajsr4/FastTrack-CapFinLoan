using CapFinLoan.Admin.Application.Contracts.Responses;

namespace CapFinLoan.Admin.Application.Interfaces;

public interface IAdminUserService
{
    Task<IReadOnlyCollection<UserSummaryResponse>> GetUsersAsync(string bearerToken, CancellationToken ct = default);
    Task<UserSummaryResponse> UpdateUserStatusAsync(Guid userId, bool isActive, string bearerToken, CancellationToken ct = default);
}

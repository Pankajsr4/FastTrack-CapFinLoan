using CapFinLoan.Admin.Application.Contracts.Requests;
using CapFinLoan.Admin.Application.Contracts.Responses;

namespace CapFinLoan.Admin.Application.Interfaces;

public interface IAdminLoanApplicationService
{
    Task<IReadOnlyCollection<AdminApplicationSummaryResponse>> GetQueueAsync(string? status, CancellationToken cancellationToken = default);
    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<AdminApplicationDetailResponse> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<AdminApplicationDetailResponse> UpdateStatusAsync(Guid applicationId, Guid reviewerUserId, ReviewLoanApplicationRequest request, CancellationToken cancellationToken = default);
    Task<AdminApplicationDetailResponse> DisburseAsync(Guid applicationId, Guid adminUserId, decimal disbursedAmount, CancellationToken cancellationToken = default);
}
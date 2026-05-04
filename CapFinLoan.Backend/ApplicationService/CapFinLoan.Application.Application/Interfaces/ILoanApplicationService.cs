using CapFinLoan.Application.Application.Contracts.Requests;
using CapFinLoan.Application.Application.Contracts.Responses;

namespace CapFinLoan.Application.Application.Interfaces;

public interface ILoanApplicationService
{
    Task<LoanApplicationResponse> CreateDraftAsync(Guid applicantUserId, SaveLoanApplicationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Update an application. Allowed only while status is in EditableStatuses.</summary>
    Task<LoanApplicationResponse> UpdateDraftAsync(Guid applicationId, Guid requesterUserId, bool isAdmin, SaveLoanApplicationRequest request, CancellationToken cancellationToken = default);

    Task<LoanApplicationResponse> GetByIdAsync(Guid applicationId, Guid requesterUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LoanApplicationResponse>> GetMineAsync(Guid applicantUserId, CancellationToken cancellationToken = default);
    Task<LoanApplicationResponse> SubmitAsync(Guid applicationId, Guid requesterUserId, CancellationToken cancellationToken = default);
    Task<LoanApplicationStatusResponse> GetStatusAsync(Guid applicationId, Guid requesterUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(Guid applicationId, Guid requesterUserId, bool isAdmin, CancellationToken cancellationToken = default);

    /// <summary>Withdraw an application. Allowed only before Under Review.</summary>
    Task<LoanApplicationResponse> WithdrawAsync(Guid applicationId, Guid applicantUserId, WithdrawApplicationRequest request, CancellationToken cancellationToken = default);
}
using CapFinLoan.Application.Domain.Entities;

namespace CapFinLoan.Application.Application.Interfaces;

public interface ILoanApplicationRepository
{
    Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default);
    Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LoanApplication>> GetByApplicantUserIdAsync(Guid applicantUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LoanApplication>> GetAllNonDraftAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default);
    Task DeleteAsync(LoanApplication application, CancellationToken cancellationToken = default);
}
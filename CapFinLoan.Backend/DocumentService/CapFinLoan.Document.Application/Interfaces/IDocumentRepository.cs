using CapFinLoan.Document.Domain.Constants;
using CapFinLoan.Document.Domain.Entities;

namespace CapFinLoan.Document.Application.Interfaces;

public interface IDocumentRepository
{
    Task<LoanDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LoanDocument>> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LoanDocument>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(LoanDocument document, CancellationToken cancellationToken = default);
    Task UpdateAsync(LoanDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only the Status and UpdatedAtUtc columns for the given document.
    /// Uses a targeted SQL UPDATE — does not load the entity into the change tracker.
    /// Returns true if a row was updated, false if the document was not found.
    /// </summary>
    Task<bool> UpdateStatusAsync(Guid documentId, DocumentStatus status, CancellationToken cancellationToken = default);
}

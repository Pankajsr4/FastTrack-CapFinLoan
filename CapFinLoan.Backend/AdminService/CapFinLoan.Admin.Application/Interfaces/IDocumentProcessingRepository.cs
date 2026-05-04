using CapFinLoan.Admin.Domain.Entities;

namespace CapFinLoan.Admin.Application.Interfaces;

public interface IDocumentProcessingRepository
{
    Task AddAsync(DocumentProcessingRecord record, CancellationToken ct = default);
    Task<DocumentProcessingRecord?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
    Task UpdateAsync(DocumentProcessingRecord record, CancellationToken ct = default);
}

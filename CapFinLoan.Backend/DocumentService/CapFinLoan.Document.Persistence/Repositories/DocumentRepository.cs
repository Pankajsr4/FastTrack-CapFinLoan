using CapFinLoan.Document.Application.Interfaces;
using CapFinLoan.Document.Domain.Constants;
using CapFinLoan.Document.Domain.Entities;
using CapFinLoan.Document.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Document.Persistence.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly DocumentDbContext _dbContext;

    public DocumentRepository(DocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LoanDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<LoanDocument>> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .Where(d => d.ApplicationId == applicationId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LoanDocument>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(LoanDocument document, CancellationToken cancellationToken = default)
    {
        await _dbContext.Documents.AddAsync(document, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Full entity update — marks all columns as modified and calls SaveChanges.
    /// Use when multiple fields change at once (e.g. file replacement, verification).
    /// </summary>
    public async Task UpdateAsync(LoanDocument document, CancellationToken cancellationToken = default)
    {
        _dbContext.Documents.Update(document);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Targeted status update — issues a single-column SQL UPDATE without loading
    /// the entity. Efficient for status transitions triggered by message consumers.
    ///
    /// SQL emitted:
    ///   UPDATE Documents
    ///   SET Status = @status, UpdatedAtUtc = @now
    ///   WHERE Id = @documentId
    /// </summary>
    public async Task<bool> UpdateStatusAsync(
        Guid documentId,
        DocumentStatus status,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rowsAffected = await _dbContext.Documents
            .Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(d => d.Status,       status)
                    .SetProperty(d => d.UpdatedAtUtc, now),
                cancellationToken);

        return rowsAffected > 0;
    }
}

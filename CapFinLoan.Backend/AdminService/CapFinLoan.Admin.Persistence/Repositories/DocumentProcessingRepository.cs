using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Entities;
using CapFinLoan.Admin.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Admin.Persistence.Repositories;

public sealed class DocumentProcessingRepository : IDocumentProcessingRepository
{
    private readonly AdminDbContext _db;

    public DocumentProcessingRepository(AdminDbContext db) => _db = db;

    public async Task AddAsync(DocumentProcessingRecord record, CancellationToken ct = default)
    {
        await _db.DocumentProcessingRecords.AddAsync(record, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<DocumentProcessingRecord?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default) =>
        _db.DocumentProcessingRecords
           .FirstOrDefaultAsync(r => r.DocumentId == documentId, ct);

    public async Task UpdateAsync(DocumentProcessingRecord record, CancellationToken ct = default)
    {
        _db.DocumentProcessingRecords.Update(record);
        await _db.SaveChangesAsync(ct);
    }
}

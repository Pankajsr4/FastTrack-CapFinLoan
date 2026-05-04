using CapFinLoan.Application.Application.Interfaces;
using CapFinLoan.Application.Domain.Entities;
using CapFinLoan.Application.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Application.Persistence.Repositories;

public class LoanApplicationRepository : ILoanApplicationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public LoanApplicationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        await _dbContext.LoanApplications.AddAsync(application, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanApplications
            .Include(x => x.StatusHistory)
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<LoanApplication>> GetByApplicantUserIdAsync(
        Guid applicantUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(x => x.ApplicantUserId == applicantUserId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LoanApplication>> GetAllNonDraftAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status != "Draft")
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(LoanApplication a, CancellationToken cancellationToken = default)
    {
        // Use SqlParameter objects so nullable values map correctly to SQL NULL.
        var sql = @"UPDATE [core].[LoanApplications] SET
            [Status]={0},[FirstName]={1},[LastName]={2},[DateOfBirth]={3},
            [Gender]={4},[Email]={5},[Phone]={6},[AddressLine1]={7},
            [AddressLine2]={8},[City]={9},[State]={10},[PostalCode]={11},
            [EmployerName]={12},[EmploymentType]={13},[MonthlyIncome]={14},
            [AnnualIncome]={15},[ExistingEmiAmount]={16},[RequestedAmount]={17},
            [RequestedTenureMonths]={18},[LoanPurpose]={19},[Remarks]={20},
            [UpdatedAtUtc]={21},[SubmittedAtUtc]={22},[IsEdited]={23},
            [EditCount]={24},[LastModifiedAt]={25},[LastModifiedBy]={26},
            [WithdrawalReason]={27},[WithdrawnAtUtc]={28}
            WHERE [Id]={29}";

        await _dbContext.Database.ExecuteSqlRawAsync(sql,
            a.Status, a.FirstName, a.LastName,
            a.DateOfBirth.HasValue ? (object)a.DateOfBirth.Value : null!,
            a.Gender, a.Email, a.Phone, a.AddressLine1, a.AddressLine2,
            a.City, a.State, a.PostalCode, a.EmployerName, a.EmploymentType,
            a.MonthlyIncome.HasValue ? (object)a.MonthlyIncome.Value : null!,
            a.AnnualIncome.HasValue  ? (object)a.AnnualIncome.Value  : null!,
            a.ExistingEmiAmount, a.RequestedAmount, a.RequestedTenureMonths,
            a.LoanPurpose, a.Remarks, a.UpdatedAtUtc,
            a.SubmittedAtUtc.HasValue   ? (object)a.SubmittedAtUtc.Value   : null!,
            a.IsEdited, a.EditCount,
            a.LastModifiedAt.HasValue   ? (object)a.LastModifiedAt.Value   : null!,
            a.LastModifiedBy   is not null ? (object)a.LastModifiedBy   : null!,
            a.WithdrawalReason is not null ? (object)a.WithdrawalReason : null!,
            a.WithdrawnAtUtc.HasValue   ? (object)a.WithdrawnAtUtc.Value   : null!,
            a.Id);

        // Collect new child records BEFORE clearing tracker.
        var newSH = a.StatusHistory
            .Where(h => _dbContext.Entry(h).State is EntityState.Detached or EntityState.Added)
            .ToList();
        var newH = a.History
            .Where(h => _dbContext.Entry(h).State is EntityState.Detached or EntityState.Added)
            .ToList();

        // Clear ALL tracked entities — prevents EF from re-saving the parent.
        _dbContext.ChangeTracker.Clear();

        // Insert child records by attaching them directly with their FK set.
        // Do NOT use AddRangeAsync (which follows navigation properties and
        // re-attaches the parent in Added state, causing PK violation).
        foreach (var sh in newSH)
        {
            sh.LoanApplicationId = a.Id;
            sh.LoanApplication = null!;   // prevent EF from re-attaching parent
            _dbContext.Entry(sh).State = EntityState.Added;
        }
        foreach (var h in newH)
        {
            h.ApplicationId = a.Id;
            h.LoanApplication = null!;    // prevent EF from re-attaching parent
            _dbContext.Entry(h).State = EntityState.Added;
        }

        if (newSH.Count > 0 || newH.Count > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        _dbContext.LoanApplications.Remove(application);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

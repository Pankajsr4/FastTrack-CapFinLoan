using CapFinLoan.Admin.Application.Contracts.Responses;
using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Constants;
using CapFinLoan.Admin.Domain.Entities;
using CapFinLoan.Admin.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Admin.Persistence.Repositories;

public class AdminLoanApplicationRepository : IAdminLoanApplicationRepository
{
    private readonly AdminDbContext _dbContext;

    public AdminLoanApplicationRepository(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LoanApplication?> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanApplications
            .Include(x => x.StatusHistory)
            .Include(x => x.Decisions)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<LoanApplication>> GetQueueAsync(string? status, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.LoanApplications.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.Trim());
        }
        else
        {
            query = query.Where(x => x.Status != ApplicationStatuses.Draft);
        }

        return await query
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LoanApplication>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status != ApplicationStatuses.Draft)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        _dbContext.LoanApplications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DashboardStatsProjection> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-11); // last 12 months inclusive
        var cutoffMonth = new DateTime(cutoff.Year, cutoff.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Round-trip 1: status counts + financial totals ────────────────────
        // Single GROUP BY query — no full entity load.
        var statusCounts = await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status != ApplicationStatuses.Draft)
            .GroupBy(x => x.Status)
            .Select(g => new StatusCount
            {
                Status = g.Key,
                Count  = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Financial aggregates — separate scalar queries are cheaper than
        // pulling all rows and summing in memory.
        var totalRequested = await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status != ApplicationStatuses.Draft)
            .SumAsync(x => x.RequestedAmount, cancellationToken);

        var totalApproved = await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status == ApplicationStatuses.Approved ||
                        x.Status == ApplicationStatuses.Disbursed)
            .SumAsync(x => x.RequestedAmount, cancellationToken);

        var totalDisbursed = await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(x => x.Status == ApplicationStatuses.Disbursed &&
                        x.DisbursedAmount != null)
            .SumAsync(x => x.DisbursedAmount ?? 0m, cancellationToken);

        // ── Round-trip 2: monthly stats (last 12 months) ─────────────────────
        // Group by Year + Month + Status in the DB; bring back only the counts.
        var monthlyRaw = await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(x => x.SubmittedAtUtc != null &&
                        x.SubmittedAtUtc >= cutoffMonth &&
                        x.Status != ApplicationStatuses.Draft)
            .GroupBy(x => new
            {
                Year   = x.SubmittedAtUtc!.Value.Year,
                Month  = x.SubmittedAtUtc!.Value.Month,
                Status = x.Status
            })
            .Select(g => new MonthlyRawEntry
            {
                Year   = g.Key.Year,
                Month  = g.Key.Month,
                Status = g.Key.Status,
                Count  = g.Count(),
                Amount = g.Sum(x => x.RequestedAmount)
            })
            .ToListAsync(cancellationToken);

        return new DashboardStatsProjection
        {
            StatusCounts   = statusCounts,
            MonthlyRaw     = monthlyRaw,
            TotalRequested = totalRequested,
            TotalApproved  = totalApproved,
            TotalDisbursed = totalDisbursed
        };
    }
}
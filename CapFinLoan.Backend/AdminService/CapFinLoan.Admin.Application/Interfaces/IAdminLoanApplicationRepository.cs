using CapFinLoan.Admin.Application.Contracts.Responses;
using CapFinLoan.Admin.Domain.Entities;

namespace CapFinLoan.Admin.Application.Interfaces;

public interface IAdminLoanApplicationRepository
{
    Task<LoanApplication?> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LoanApplication>> GetQueueAsync(string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LoanApplication>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns status-grouped counts + monthly submission stats in two DB round-trips.
    /// Avoids loading full entity graphs into memory.
    /// </summary>
    Task<DashboardStatsProjection> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Raw projection returned by the repository — no business logic.</summary>
public sealed class DashboardStatsProjection
{
    public IReadOnlyList<StatusCount>       StatusCounts  { get; init; } = [];
    public IReadOnlyList<MonthlyRawEntry>   MonthlyRaw    { get; init; } = [];
    public decimal                          TotalRequested { get; init; }
    public decimal                          TotalApproved  { get; init; }
    public decimal                          TotalDisbursed { get; init; }
}

public sealed class StatusCount
{
    public string Status { get; init; } = string.Empty;
    public int    Count  { get; init; }
}

public sealed class MonthlyRawEntry
{
    public int     Year   { get; init; }
    public int     Month  { get; init; }
    public string  Status { get; init; } = string.Empty;
    public int     Count  { get; init; }
    public decimal Amount { get; init; }
}
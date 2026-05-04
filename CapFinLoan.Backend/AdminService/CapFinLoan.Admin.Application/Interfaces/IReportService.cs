using CapFinLoan.Admin.Application.Contracts.Responses;

namespace CapFinLoan.Admin.Application.Interfaces;

public interface IReportService
{
    Task<ReportSummaryResponse> GetSummaryAsync(CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
}

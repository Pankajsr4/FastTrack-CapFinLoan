using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.Admin.API.Controllers;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = RoleNames.Admin)]
public class AdminReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public AdminReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Returns a summary of all loan applications grouped by status.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _reportService.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    /// <summary>Exports all loan applications as a downloadable CSV file.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(CancellationToken cancellationToken)
    {
        var csvBytes = await _reportService.ExportCsvAsync(cancellationToken);

        var fileName = $"capfinloan-applications-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

        return File(
            fileContents: csvBytes,
            contentType: "text/csv",
            fileDownloadName: fileName);
    }
}

using System.Security.Claims;
using CapFinLoan.Admin.Application.Contracts.Requests;
using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.Admin.API.Controllers;

[ApiController]
[Route("api/admin/applications")]
[Authorize(Roles = RoleNames.Admin)]
public class AdminApplicationsController : ControllerBase
{
    private readonly IAdminLoanApplicationService _adminLoanApplicationService;

    public AdminApplicationsController(IAdminLoanApplicationService adminLoanApplicationService)
    {
        _adminLoanApplicationService = adminLoanApplicationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetQueue([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var applications = await _adminLoanApplicationService.GetQueueAsync(status, cancellationToken);
        return Ok(applications);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _adminLoanApplicationService.GetDashboardAsync(cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        // KeyNotFoundException → 404 via GlobalExceptionMiddleware
        var application = await _adminLoanApplicationService.GetByIdAsync(id, cancellationToken);
        return Ok(application);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] ReviewLoanApplicationRequest request,
        CancellationToken cancellationToken)
    {
        // FluentValidation validates TargetStatus before this runs.
        // KeyNotFoundException → 404, InvalidOperationException → 400 via GlobalExceptionMiddleware.
        var application = await _adminLoanApplicationService.UpdateStatusAsync(id, GetUserId(), request, cancellationToken);
        return Ok(application);
    }

    [HttpPost("{id:guid}/disburse")]
    public async Task<IActionResult> Disburse(
        Guid id,
        [FromBody] DisburseApplicationRequest request,
        CancellationToken cancellationToken)
    {
        // Only Approved → Disbursed is allowed; service throws InvalidOperationException otherwise.
        var application = await _adminLoanApplicationService.DisburseAsync(id, GetUserId(), request.DisbursedAmount, cancellationToken);
        return Ok(application);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }
}

using CapFinLoan.Application.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.Application.API.Controllers;

/// <summary>
/// Internal endpoint — called by AdminService to sync applications into its own DB.
/// Only accessible from within the Docker network (no public gateway route).
/// </summary>
[ApiController]
[Route("api/internal/applications")]
[Authorize]
public class InternalApplicationsController : ControllerBase
{
    private readonly ILoanApplicationRepository _repo;

    public InternalApplicationsController(ILoanApplicationRepository repo)
        => _repo = repo;

    /// <summary>
    /// Returns all non-Draft applications for admin sync.
    /// Used by AdminService to backfill its own DB.
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var apps = await _repo.GetAllNonDraftAsync(ct);
        return Ok(apps.Select(a => new
        {
            a.Id,
            a.ApplicantUserId,
            a.ApplicationNumber,
            a.Status,
            FirstName      = a.FirstName,
            LastName       = a.LastName,
            a.Email,
            a.Phone,
            a.AddressLine1,
            a.AddressLine2,
            a.City,
            a.State,
            a.PostalCode,
            a.EmployerName,
            a.EmploymentType,
            a.MonthlyIncome,
            a.AnnualIncome,
            a.ExistingEmiAmount,
            a.RequestedAmount,
            a.RequestedTenureMonths,
            a.LoanPurpose,
            a.Remarks,
            a.CreatedAtUtc,
            a.UpdatedAtUtc,
            a.SubmittedAtUtc
        }));
    }
}

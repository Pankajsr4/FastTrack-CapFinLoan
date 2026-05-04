using CapFinLoan.Admin.Application.Contracts.Requests;
using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.Admin.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.Admin)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _userService;

    public AdminUsersController(IAdminUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Returns all registered users with their roles and active status.</summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _userService.GetUsersAsync(GetBearerToken(), cancellationToken);
        return Ok(users);
    }

    /// <summary>Activates or deactivates a user account.</summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        // KeyNotFoundException → 404 via GlobalExceptionMiddleware
        var user = await _userService.UpdateUserStatusAsync(id, request.IsActive, GetBearerToken(), cancellationToken);
        return Ok(user);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Extracts the raw JWT from the incoming Authorization header to forward to AuthService.</summary>
    private string GetBearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return header["Bearer ".Length..].Trim();

        throw new UnauthorizedAccessException("Authorization header is missing or malformed.");
    }
}

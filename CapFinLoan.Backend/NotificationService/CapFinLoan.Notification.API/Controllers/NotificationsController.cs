using CapFinLoan.Notification.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.Notification.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repo;

    public NotificationsController(INotificationRepository repo) => _repo = repo;

    /// <summary>Get all notifications for a user.</summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetByUser(Guid userId, CancellationToken ct)
    {
        var notifications = await _repo.GetByUserIdAsync(userId, ct);
        return Ok(notifications.Select(n => new
        {
            n.Id,
            n.Type,
            n.Title,
            n.Message,
            n.ApplicationNumber,
            isRead    = n.IsRead,
            createdAt = n.CreatedAtUtc
        }));
    }

    /// <summary>Mark a notification as read.</summary>
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _repo.MarkAsReadAsync(id, ct);
        return NoContent();
    }

    /// <summary>Health check — no auth required.</summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "Notification Service running" });
}

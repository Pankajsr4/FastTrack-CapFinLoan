namespace CapFinLoan.Notification.Application.Entities;

public sealed class NotificationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;   // Submitted | Approved | Rejected | Updated
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

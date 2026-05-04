using CapFinLoan.Notification.Application.Entities;

namespace CapFinLoan.Notification.Application.Interfaces;

/// <summary>Pushes real-time notifications to connected clients (SignalR).</summary>
public interface INotificationPusher
{
    Task PushAsync(Guid userId, NotificationRecord record, CancellationToken ct = default);
}

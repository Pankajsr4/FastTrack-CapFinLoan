using CapFinLoan.Notification.Application.Entities;

namespace CapFinLoan.Notification.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(NotificationRecord record, CancellationToken ct = default);
    Task<IReadOnlyCollection<NotificationRecord>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default);
}

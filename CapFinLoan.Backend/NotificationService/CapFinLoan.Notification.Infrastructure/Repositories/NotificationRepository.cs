using CapFinLoan.Notification.Application.Entities;
using CapFinLoan.Notification.Application.Interfaces;
using CapFinLoan.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Notification.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;

    public NotificationRepository(NotificationDbContext db) => _db = db;

    public async Task AddAsync(NotificationRecord record, CancellationToken ct = default)
    {
        await _db.Notifications.AddAsync(record, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<NotificationRecord>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        await _db.Notifications
            .Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}

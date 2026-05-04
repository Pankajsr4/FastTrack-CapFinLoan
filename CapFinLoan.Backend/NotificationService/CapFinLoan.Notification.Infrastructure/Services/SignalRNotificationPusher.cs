using CapFinLoan.Notification.Application.Entities;
using CapFinLoan.Notification.Application.Interfaces;
using CapFinLoan.Notification.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Notification.Infrastructure.Services;

public sealed class SignalRNotificationPusher : INotificationPusher
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationPusher> _logger;

    public SignalRNotificationPusher(
        IHubContext<NotificationHub> hub,
        ILogger<SignalRNotificationPusher> logger)
    {
        _hub    = hub;
        _logger = logger;
    }

    public async Task PushAsync(Guid userId, NotificationRecord record, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients
                .Group($"user-{userId}")
                .SendAsync("ReceiveNotification", new
                {
                    id                = record.Id,
                    type              = record.Type,
                    title             = record.Title,
                    message           = record.Message,
                    applicationNumber = record.ApplicationNumber,
                    createdAt         = record.CreatedAtUtc,
                    isRead            = record.IsRead
                }, ct);

            _logger.LogDebug("[SignalR] Pushed {Type} notification to user {UserId}", record.Type, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SignalR] Failed to push notification to user {UserId}", userId);
        }
    }
}

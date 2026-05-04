using Microsoft.AspNetCore.SignalR;

namespace CapFinLoan.Notification.Infrastructure.Hubs;

/// <summary>
/// SignalR hub — clients join a group named after their userId
/// and receive real-time notification pushes.
/// </summary>
public sealed class NotificationHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }

    public async Task LeaveUserGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
    }
}

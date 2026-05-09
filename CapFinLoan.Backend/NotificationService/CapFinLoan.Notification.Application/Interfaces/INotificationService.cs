namespace CapFinLoan.Notification.Application.Interfaces;

public interface INotificationService
{
    Task SendApplicationSubmittedAsync(Guid applicationId, Guid applicantUserId, string applicationNumber, string applicantName, string email, decimal amount, CancellationToken ct = default);
    Task SendApplicationApprovedAsync(Guid applicationId, Guid applicantUserId, string applicationNumber, string applicantName, string email, decimal amount, CancellationToken ct = default);
    Task SendApplicationRejectedAsync(Guid applicationId, Guid applicantUserId, string applicationNumber, string applicantName, string email, string remarks, CancellationToken ct = default);
    Task SendApplicationUpdatedAsync(Guid applicationId, Guid applicantUserId, string applicationNumber, string previousStatus, string newStatus, string remarks, CancellationToken ct = default);
}

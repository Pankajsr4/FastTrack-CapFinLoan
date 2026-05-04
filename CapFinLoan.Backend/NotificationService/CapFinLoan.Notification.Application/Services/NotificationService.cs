using CapFinLoan.Notification.Application.Entities;
using CapFinLoan.Notification.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Notification.Application.Services;

/// <summary>
/// Handles notification delivery: logs, simulates email, stores in DB,
/// and pushes real-time updates via SignalR.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _repo;
    private readonly INotificationPusher _pusher;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repo,
        INotificationPusher pusher,
        ILogger<NotificationService> logger)
    {
        _repo   = repo;
        _pusher = pusher;
        _logger = logger;
    }

    public async Task SendApplicationSubmittedAsync(
        Guid applicationId, Guid applicantUserId,
        string applicationNumber,
        string applicantName, string email,
        decimal amount, CancellationToken ct = default)
    {
        var title   = $"Application {applicationNumber} Received";
        var message = $"Your loan application for ₹{amount:N0} has been received and is under review.";

        _logger.LogInformation("[NOTIFICATION] Submitted | {Ref} | {Name} | {Email}", applicationNumber, applicantName, email);
        SimulateEmail(email, $"[CapFinLoan] {title}", $"Dear {applicantName},\n\n{message}\n\nThank you.");

        var record = new NotificationRecord
        {
            UserId            = applicantUserId,
            ApplicationNumber = applicationNumber,
            Type              = "Submitted",
            Title             = title,
            Message           = message
        };
        await _repo.AddAsync(record, ct);
        await _pusher.PushAsync(record.UserId, record, ct);
    }

    public async Task SendApplicationApprovedAsync(
        Guid applicationId, string applicationNumber,
        string applicantName, string email,
        decimal amount, CancellationToken ct = default)
    {
        var title   = $"Application {applicationNumber} Approved 🎉";
        var message = $"Congratulations! Your loan application for ₹{amount:N0} has been approved.";

        _logger.LogInformation("[NOTIFICATION] Approved | {Ref} | {Name}", applicationNumber, applicantName);
        SimulateEmail(email, $"[CapFinLoan] {title}", $"Dear {applicantName},\n\n{message}\n\nOur team will contact you shortly.");

        var record = new NotificationRecord
        {
            UserId            = applicationId,
            ApplicationNumber = applicationNumber,
            Type              = "Approved",
            Title             = title,
            Message           = message
        };
        await _repo.AddAsync(record, ct);
        await _pusher.PushAsync(record.UserId, record, ct);
    }

    public async Task SendApplicationRejectedAsync(
        Guid applicationId, string applicationNumber,
        string applicantName, string email,
        string remarks, CancellationToken ct = default)
    {
        var title   = $"Application {applicationNumber} Rejected";
        var message = $"Your loan application was not approved. Reason: {remarks}";

        _logger.LogInformation("[NOTIFICATION] Rejected | {Ref} | {Name}", applicationNumber, applicantName);
        SimulateEmail(email, $"[CapFinLoan] {title}", $"Dear {applicantName},\n\n{message}\n\nYou may reapply after addressing the above.");

        var record = new NotificationRecord
        {
            UserId            = applicationId,
            ApplicationNumber = applicationNumber,
            Type              = "Rejected",
            Title             = title,
            Message           = message
        };
        await _repo.AddAsync(record, ct);
        await _pusher.PushAsync(record.UserId, record, ct);
    }

    public async Task SendApplicationUpdatedAsync(
        Guid applicationId, string applicationNumber,
        string previousStatus, string newStatus,
        string remarks, CancellationToken ct = default)
    {
        var title   = $"Application {applicationNumber} Status Updated";
        var message = $"Status changed from '{previousStatus}' to '{newStatus}'. {remarks}".Trim();

        _logger.LogInformation("[NOTIFICATION] Updated | {Ref} | {From} → {To}", applicationNumber, previousStatus, newStatus);
        SimulateEmail($"applicant-{applicationId}@capfinloan.internal", $"[CapFinLoan] {title}", message);

        var record = new NotificationRecord
        {
            UserId            = applicationId,
            ApplicationNumber = applicationNumber,
            Type              = "Updated",
            Title             = title,
            Message           = message
        };
        await _repo.AddAsync(record, ct);
        await _pusher.PushAsync(record.UserId, record, ct);
    }

    private void SimulateEmail(string to, string subject, string body)
    {
        _logger.LogInformation(
            "[EMAIL SIMULATION]\n  To:      {To}\n  Subject: {Subject}\n  Body:\n{Body}",
            to, subject, body);
    }
}

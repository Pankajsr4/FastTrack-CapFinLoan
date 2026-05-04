using CapFinLoan.Document.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CapFinLoan.Document.API.Hubs;

/// <summary>
/// Implements IDocumentStatusNotifier (Application layer) using SignalR IHubContext.
/// Registered as Scoped — safe to inject into DocumentService (also Scoped).
/// </summary>
public sealed class DocumentStatusNotifier : IDocumentStatusNotifier
{
    private readonly IHubContext<DocumentStatusHub> _hub;
    private readonly ILogger<DocumentStatusNotifier> _logger;

    public DocumentStatusNotifier(
        IHubContext<DocumentStatusHub> hub,
        ILogger<DocumentStatusNotifier> logger)
    {
        _hub    = hub;
        _logger = logger;
    }

    public async Task NotifyStatusChangedAsync(
        Guid    documentId,
        string  status,
        string? failureReason = null,
        CancellationToken ct  = default)
    {
        var groupName = DocumentStatusHub.GroupName(documentId.ToString());

        var payload = new
        {
            documentId    = documentId,
            status        = status,
            updatedAt     = DateTime.UtcNow,
            failureReason = failureReason,
        };

        await _hub.Clients.Group(groupName)
            .SendAsync("DocumentStatusUpdated", payload, ct);

        _logger.LogDebug(
            "SignalR: pushed DocumentStatusUpdated to group {Group} — status={Status}",
            groupName, status);
    }
}

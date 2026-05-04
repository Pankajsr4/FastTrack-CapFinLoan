using Microsoft.AspNetCore.SignalR;

namespace CapFinLoan.Document.API.Hubs;

/// <summary>
/// SignalR hub that pushes document status updates to connected clients.
///
/// Clients join a group named after the documentId they want to track.
/// When a status transition occurs, the server calls:
///   connection.on("DocumentStatusUpdated", (update) => { ... })
///
/// Message shape:
/// {
///   "documentId": "guid",
///   "status":     "Processing",
///   "updatedAt":  "2026-04-02T10:15:00Z",
///   "failureReason": null
/// }
/// </summary>
public class DocumentStatusHub : Hub
{
    private readonly ILogger<DocumentStatusHub> _logger;

    public DocumentStatusHub(ILogger<DocumentStatusHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client calls this after connecting to subscribe to updates for a document.
    /// The client is added to a SignalR group named by the documentId.
    /// </summary>
    public async Task SubscribeToDocument(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(documentId));

        _logger.LogDebug(
            "SignalR: connection {ConnectionId} subscribed to document {DocumentId}",
            Context.ConnectionId, documentId);
    }

    /// <summary>
    /// Client calls this to stop receiving updates for a document.
    /// </summary>
    public async Task UnsubscribeFromDocument(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(documentId));

        _logger.LogDebug(
            "SignalR: connection {ConnectionId} unsubscribed from document {DocumentId}",
            Context.ConnectionId, documentId);
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("SignalR: client connected — {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("SignalR: client disconnected — {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Group name convention — one group per document.</summary>
    public static string GroupName(string documentId) => $"doc:{documentId}";
}

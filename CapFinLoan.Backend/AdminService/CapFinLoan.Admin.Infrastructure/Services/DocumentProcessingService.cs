using System.Diagnostics;
using System.Net.Http.Json;
using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Entities;
using CapFinLoan.Messaging.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Admin.Infrastructure.Services;

/// <summary>
/// Full document processing pipeline with structured logging at every stage.
///
/// Log events emitted:
///   START   — ProcessDocumentAsync called, all input fields logged
///   STAGE   — status transition at each pipeline step (Debug)
///   SUCCESS — pipeline completed, document queued for review, elapsed time
///   FAILURE — exception type, message, elapsed time, failure reason persisted
/// </summary>
public sealed partial class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IDocumentProcessingRepository   _repository;
    private readonly IHttpClientFactory              _httpClientFactory;
    private readonly ILogger<DocumentProcessingService> _logger;

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png",
        };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public DocumentProcessingService(
        IDocumentProcessingRepository      repository,
        IHttpClientFactory                 httpClientFactory,
        ILogger<DocumentProcessingService> logger)
    {
        _repository        = repository;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    public async Task ProcessDocumentAsync(
        DocumentUploadedEvent evt,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // ── Validate ──────────────────────────────────────────────────────────
        try
        {
            Validate(evt);
        }
        catch (Exception ex)
        {
            Log.ValidationFailed(_logger, ex, evt.DocumentId, ex.Message);
            throw; // permanent failure → dead-lettered by RabbitMqConsumer
        }

        // ── START log ─────────────────────────────────────────────────────────
        Log.ProcessingStart(_logger,
            evt.DocumentId, evt.ApplicationId, evt.UserId,
            evt.DocumentType, evt.FileName, evt.ContentType,
            evt.FileSizeBytes / 1024.0, evt.UploadedAtUtc);

        // ── Persist initial record (idempotent) ──────────────────────────────
        // On retry the record already exists (DocumentId has a unique index).
        // Load it and resume from its current status rather than inserting again.
        var record = await _repository.GetByDocumentIdAsync(evt.DocumentId, cancellationToken);

        if (record is not null)
        {
            // Already processed successfully — ack without reprocessing.
            if (record.Status == DocumentProcessingStatus.UnderReview)
            {
                _logger.LogInformation(
                    "[DocumentProcessing] Idempotent skip — DocumentId: {DocumentId} " +
                    "already in status '{Status}'. Acking without reprocessing.",
                    evt.DocumentId, record.Status);
                return;
            }

            // Previously failed — reset to Received and retry the pipeline.
            _logger.LogInformation(
                "[DocumentProcessing] Resuming retry — DocumentId: {DocumentId} " +
                "previous status: '{Status}'.",
                evt.DocumentId, record.Status);

            record.Status        = DocumentProcessingStatus.Received;
            record.FailureReason = null;
            record.UpdatedAtUtc  = DateTime.UtcNow;
            await _repository.UpdateAsync(record, cancellationToken);
        }
        else
        {
            record = new DocumentProcessingRecord
            {
                DocumentId    = evt.DocumentId,
                ApplicationId = evt.ApplicationId,
                UserId        = evt.UserId,
                DocumentType  = evt.DocumentType,
                FileName      = evt.FileName,
                ContentType   = evt.ContentType,
                FileSizeBytes = evt.FileSizeBytes,
                Status        = DocumentProcessingStatus.Received,
                ReceivedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc  = DateTime.UtcNow,
            };
            await _repository.AddAsync(record, cancellationToken);
        }

        Log.RecordCreated(_logger, evt.DocumentId, record.Id);

        try
        {
            // ── Step 1-2: Validating ──────────────────────────────────────────
            // Save: AdminService record → Validating
            await SetStageAsync(record, DocumentProcessingStatus.Validating, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken); // placeholder
            Log.StageComplete(_logger, evt.DocumentId, DocumentProcessingStatus.Validating);

            // ── Step 3-4: Processing ──────────────────────────────────────────
            // Save: DocumentService document → Processing (via HTTP)
            await MarkDocumentStatusAsync(evt.DocumentId, "Processing", cancellationToken);
            // Save: AdminService record → Processing
            await SetStageAsync(record, DocumentProcessingStatus.Processing, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken); // placeholder
            Log.StageComplete(_logger, evt.DocumentId, DocumentProcessingStatus.Processing);

            // ── Step 6 (success): Completed ───────────────────────────────────
            // Save: DocumentService document → Completed (via HTTP)
            await MarkDocumentStatusAsync(evt.DocumentId, "Completed", cancellationToken);
            // Save: AdminService record → Completed
            await SetStageAsync(record, DocumentProcessingStatus.Completed, cancellationToken);
            Log.StageComplete(_logger, evt.DocumentId, DocumentProcessingStatus.Completed);

            // ── Transition to admin review queue ──────────────────────────────
            // Save: DocumentService document → UnderReview (via HTTP)
            await MarkUnderReviewAsync(evt.DocumentId, cancellationToken);
            // Save: AdminService record → UnderReview + ProcessedAtUtc
            record.Status         = DocumentProcessingStatus.UnderReview;
            record.ProcessedAtUtc = DateTime.UtcNow;
            record.UpdatedAtUtc   = DateTime.UtcNow;
            await _repository.UpdateAsync(record, cancellationToken);

            sw.Stop();
            Log.ProcessingSuccess(_logger, evt.DocumentId, record.Id, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var failureReason = $"{ex.GetType().Name}: {ex.Message}";

            Log.ProcessingFailed(_logger, ex,
                evt.DocumentId, ex.GetType().Name, ex.Message, sw.ElapsedMilliseconds);

            // ── Step 7 (failure): Failed ──────────────────────────────────────
            // Save: AdminService record → Failed
            // CancellationToken.None — original token may already be cancelled
            record.Status        = DocumentProcessingStatus.Failed;
            record.FailureReason = failureReason;
            record.UpdatedAtUtc  = DateTime.UtcNow;

            try
            {
                await _repository.UpdateAsync(record, CancellationToken.None);
                Log.FailureRecordPersisted(_logger, evt.DocumentId, record.Id);
            }
            catch (Exception dbEx)
            {
                Log.FailureRecordPersistError(_logger, dbEx, evt.DocumentId);
            }

            // Save: DocumentService document → Failed (via HTTP)
            // Skip for validation errors — the document may not exist in DocumentService
            if (ex is not ArgumentException)
            {
                try
                {
                    await MarkDocumentFailedAsync(evt.DocumentId, failureReason, CancellationToken.None);
                }
                catch (Exception notifyEx)
                {
                    Log.FailureNotifyError(_logger, notifyEx, evt.DocumentId);
                }
            }

            throw; // propagate → RabbitMqConsumer nacks and retries / dead-letters
        }
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static void Validate(DocumentUploadedEvent evt)
    {
        if (evt.DocumentId == Guid.Empty)
            throw new ArgumentException("DocumentId must not be empty.", nameof(evt));
        if (evt.ApplicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId must not be empty.", nameof(evt));
        if (evt.UserId == Guid.Empty)
            throw new ArgumentException("UserId must not be empty.", nameof(evt));
        if (string.IsNullOrWhiteSpace(evt.FileName))
            throw new ArgumentException("FileName (FileUrl) must not be empty.", nameof(evt));
        if (string.IsNullOrWhiteSpace(evt.DocumentType))
            throw new ArgumentException("DocumentType must not be empty.", nameof(evt));
        if (!AllowedContentTypes.Contains(evt.ContentType))
            throw new InvalidOperationException(
                $"Unsupported content type '{evt.ContentType}'. " +
                $"Allowed: {string.Join(", ", AllowedContentTypes)}");
        if (evt.FileSizeBytes <= 0 || evt.FileSizeBytes > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"File size {evt.FileSizeBytes} bytes is out of range (1 – {MaxFileSizeBytes} bytes).");
    }

    private async Task SetStageAsync(DocumentProcessingRecord record, string status, CancellationToken ct)
    {
        record.Status       = status;
        record.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(record, ct);
        Log.StageSet(_logger, record.DocumentId, status);
    }

    private async Task MarkUnderReviewAsync(Guid documentId, CancellationToken ct)
    {
        await MarkDocumentStatusAsync(documentId, "UnderReview", ct);
    }

    /// <summary>
    /// Generic helper — PATCH /internal/documents/{id}/status { "status": "{targetStatus}" }
    /// Throws HttpRequestException on non-2xx → classified as transient → nack + retry.
    /// </summary>
    private async Task MarkDocumentStatusAsync(Guid documentId, string targetStatus, CancellationToken ct)
    {
        var client   = _httpClientFactory.CreateClient("DocumentServiceClient");
        var response = await client.PatchAsJsonAsync(
            $"/api/internal/documents/{documentId}/status",
            new { status = targetStatus }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            Log.HttpCallFailed(_logger, documentId, targetStatus, (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"DocumentService status update to '{targetStatus}' failed ({(int)response.StatusCode}): {body}");
        }

        _logger.LogDebug(
            "[DocumentProcessing] DocumentService status updated — DocumentId: {DocumentId}, Status: {Status}",
            documentId, targetStatus);
    }

    private async Task MarkDocumentFailedAsync(Guid documentId, string failureReason, CancellationToken ct)
    {
        var client   = _httpClientFactory.CreateClient("DocumentServiceClient");
        var response = await client.PatchAsJsonAsync(
            $"/api/internal/documents/{documentId}/status",
            new { status = "Failed", failureReason }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            Log.HttpCallFailed(_logger, documentId, "Failed", (int)response.StatusCode, body);
        }
        else
        {
            Log.DocumentMarkedFailed(_logger, documentId);
        }
    }

    // =========================================================================
    // LoggerMessage source-generated delegates — zero allocation on hot paths
    // =========================================================================

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Processing started — " +
                      "DocumentId: {DocumentId} | ApplicationId: {ApplicationId} | UserId: {UserId} | " +
                      "Type: {DocumentType} | File: {FileName} ({ContentType}, {SizeKb:F1} KB) | " +
                      "UploadedAt: {UploadedAt:u}")]
        public static partial void ProcessingStart(ILogger logger,
            Guid documentId, Guid applicationId, Guid userId,
            string documentType, string fileName, string contentType,
            double sizeKb, DateTime uploadedAt);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Processing record created — DocumentId: {DocumentId}, RecordId: {RecordId}")]
        public static partial void RecordCreated(ILogger logger, Guid documentId, Guid recordId);

        [LoggerMessage(Level = LogLevel.Debug,
            Message = "Processing stage set — DocumentId: {DocumentId}, Status: {Status}")]
        public static partial void StageSet(ILogger logger, Guid documentId, string status);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Processing stage complete — DocumentId: {DocumentId}, Stage: {Stage}")]
        public static partial void StageComplete(ILogger logger, Guid documentId, string stage);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Processing completed — DocumentId: {DocumentId} queued for review " +
                      "(RecordId: {RecordId}) in {ElapsedMs}ms")]
        public static partial void ProcessingSuccess(ILogger logger,
            Guid documentId, Guid recordId, long elapsedMs);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "Processing failed — DocumentId: {DocumentId} | " +
                      "{ExceptionType}: {ExceptionMessage} | elapsed: {ElapsedMs}ms")]
        public static partial void ProcessingFailed(ILogger logger, Exception ex,
            Guid documentId, string exceptionType, string exceptionMessage, long elapsedMs);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Processing failed (validation) — DocumentId: {DocumentId}: {Reason}")]
        public static partial void ValidationFailed(ILogger logger, Exception ex,
            Guid documentId, string reason);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Processing failure persisted — DocumentId: {DocumentId}, RecordId: {RecordId}")]
        public static partial void FailureRecordPersisted(ILogger logger, Guid documentId, Guid recordId);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "Processing failed — could not persist failure record for DocumentId: {DocumentId}")]
        public static partial void FailureRecordPersistError(ILogger logger, Exception ex, Guid documentId);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "Processing failed — could not notify DocumentService for DocumentId: {DocumentId}")]
        public static partial void FailureNotifyError(ILogger logger, Exception ex, Guid documentId);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Processing failed — HTTP call to DocumentService failed | DocumentId: {DocumentId}, " +
                      "TargetStatus: {TargetStatus}, StatusCode: {StatusCode}, Body: {Body}")]
        public static partial void HttpCallFailed(ILogger logger,
            Guid documentId, string targetStatus, int statusCode, string body);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Processing failed — DocumentService marked document {DocumentId} as Failed")]
        public static partial void DocumentMarkedFailed(ILogger logger, Guid documentId);
    }
}

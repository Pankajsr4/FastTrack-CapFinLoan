using CapFinLoan.Document.Application.Contracts.Responses;
using CapFinLoan.Document.Application.Interfaces;
using CapFinLoan.Document.Domain.Constants;
using CapFinLoan.Document.Domain.Entities;
using CapFinLoan.Messaging.Contracts.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace CapFinLoan.Document.Application.Services;

public sealed partial class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher     _eventPublisher;
    private readonly ILogger<DocumentService> _logger;
    private readonly IDocumentStatusNotifier? _notifier; // null when not registered (e.g. tests)

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public DocumentService(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        IEventPublisher     eventPublisher,
        ILogger<DocumentService> logger,
        IDocumentStatusNotifier? notifier = null)
    {
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
        _eventPublisher     = eventPublisher;
        _logger             = logger;
        _notifier           = notifier;
    }

    public async Task<DocumentResponse> UploadAsync(Guid userId, Guid applicationId, string documentType, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            throw new InvalidOperationException("File is empty.");

        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"File size exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("File type is not supported. Allowed types: PDF, JPG, PNG.");

        await using var stream = file.OpenReadStream();
        var storedFileName = await _fileStorageService.SaveFileAsync(stream, file.FileName, cancellationToken);

        var document = new LoanDocument
        {
            ApplicationId = applicationId,
            UserId = userId,
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            DocumentType = documentType,
            Status = DocumentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _documentRepository.AddAsync(document, cancellationToken);

        await _eventPublisher.PublishAsync(new DocumentUploadedEvent
        {
            DocumentId = document.Id,
            ApplicationId = document.ApplicationId,
            UserId = document.UserId,
            DocumentType = document.DocumentType,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,
            UploadedAtUtc = document.CreatedAtUtc
        }, cancellationToken);

        return MapToResponse(document);
    }

    public async Task<DocumentResponse> ReplaceAsync(Guid userId, Guid documentId, IFormFile file, string? documentType = null, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            throw new InvalidOperationException("File is empty.");

        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"File size exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("File type is not supported. Allowed types: PDF, JPG, PNG.");

        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                       ?? throw new KeyNotFoundException("Document not found.");

        if (document.UserId != userId)
            throw new UnauthorizedAccessException("You are not allowed to edit this document.");

        await using var stream = file.OpenReadStream();
        var storedFileName = await _fileStorageService.SaveFileAsync(stream, file.FileName, cancellationToken);

        document.FileName = file.FileName;
        document.StoredFileName = storedFileName;
        document.ContentType = file.ContentType;
        document.FileSizeBytes = file.Length;
        document.DocumentType = string.IsNullOrWhiteSpace(documentType) ? document.DocumentType : documentType;
        document.Status = DocumentStatus.Pending;  // Reset to pending on re-upload
        document.VerifiedByUserId = null;
        document.VerifiedAtUtc = null;
        document.Remarks = null;
        document.UpdatedAtUtc = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(document, cancellationToken);
        return MapToResponse(document);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetByApplicationIdAsync(applicationId, cancellationToken);
        return documents.Select(MapToResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetByUserIdAsync(userId, cancellationToken);
        return documents.Select(MapToResponse).ToArray();
    }

    public async Task<DocumentResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken)
                       ?? throw new KeyNotFoundException("Document not found.");
        return MapToResponse(document);
    }

    public async Task<Stream?> GetFileStreamAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                       ?? throw new KeyNotFoundException("Document not found.");
        return await _fileStorageService.GetFileStreamAsync(document.StoredFileName, cancellationToken);
    }

    public async Task<DocumentResponse> VerifyAsync(Guid documentId, Guid adminUserId, bool isVerified, string? remarks, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                       ?? throw new KeyNotFoundException("Document not found.");

        document.Status = isVerified ? DocumentStatus.Verified : DocumentStatus.ReuploadRequired;
        document.VerifiedByUserId = adminUserId;
        document.VerifiedAtUtc = isVerified ? DateTime.UtcNow : null;
        document.Remarks = remarks;
        document.UpdatedAtUtc = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(document, cancellationToken);

        await _eventPublisher.PublishAsync(new DocumentVerifiedEvent
        {
            DocumentId = document.Id,
            ApplicationId = document.ApplicationId,
            UserId = document.UserId,
            DocumentType = document.DocumentType,
            FileName = document.FileName,
            IsVerified = document.IsVerified,
            Remarks = document.Remarks,
            VerifiedByUserId = adminUserId,
            VerifiedAtUtc = document.VerifiedAtUtc ?? DateTime.UtcNow
        }, cancellationToken);

        await _eventPublisher.PublishAsync(new DocumentProcessedEvent
        {
            DocumentId = document.Id,
            ApplicationId = document.ApplicationId,
            UserId = document.UserId,
            DocumentType = document.DocumentType,
            ProcessedStatus = document.Status.ToString(),
            ProcessedByUserId = adminUserId,
            ProcessedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        return MapToResponse(document);
    }

    public async Task<DocumentResponse> MarkProcessingAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        Log.StatusTransitionStart(_logger, documentId, DocumentStatus.Processing.ToString());

        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                       ?? throw new KeyNotFoundException("Document not found.");

        // Idempotent — only transition from Pending
        if (document.Status != DocumentStatus.Pending)
        {
            Log.StatusTransitionSkipped(_logger, documentId, document.Status.ToString(), DocumentStatus.Processing.ToString());
            return MapToResponse(document);
        }

        var updated = await _documentRepository.UpdateStatusAsync(
            documentId, DocumentStatus.Processing, cancellationToken);

        if (!updated)
            throw new KeyNotFoundException("Document not found during status update.");

        document.Status       = DocumentStatus.Processing;
        document.UpdatedAtUtc = DateTime.UtcNow;

        Log.StatusTransitionSuccess(_logger, documentId, DocumentStatus.Processing.ToString());
        if (_notifier is not null)
            await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.Processing.ToString(), ct: cancellationToken);
        return MapToResponse(document);
    }

    public async Task<DocumentResponse> MarkCompletedAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        Log.StatusTransitionStart(_logger, documentId, DocumentStatus.Completed.ToString());

        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                       ?? throw new KeyNotFoundException("Document not found.");

        // Idempotent — only transition from Processing
        if (document.Status != DocumentStatus.Processing)
        {
            Log.StatusTransitionSkipped(_logger, documentId, document.Status.ToString(), DocumentStatus.Completed.ToString());
            return MapToResponse(document);
        }

        var updated = await _documentRepository.UpdateStatusAsync(
            documentId, DocumentStatus.Completed, cancellationToken);

        if (!updated)
            throw new KeyNotFoundException("Document not found during status update.");

        document.Status       = DocumentStatus.Completed;
        document.UpdatedAtUtc = DateTime.UtcNow;

        Log.StatusTransitionSuccess(_logger, documentId, DocumentStatus.Completed.ToString());
        if (_notifier is not null)
            await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.Completed.ToString(), ct: cancellationToken);
        return MapToResponse(document);
    }

    public async Task<DocumentResponse> MarkUnderReviewAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        Log.StatusTransitionStart(_logger, documentId, DocumentStatus.UnderReview.ToString());

        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                       ?? throw new KeyNotFoundException("Document not found.");

        // Accept transition from Pending (legacy path) or Completed (new pipeline path)
        var validSources = new[] { DocumentStatus.Pending, DocumentStatus.Completed };
        if (!validSources.Contains(document.Status))
        {
            Log.StatusTransitionSkipped(_logger, documentId, document.Status.ToString(), DocumentStatus.UnderReview.ToString());
            return MapToResponse(document);
        }

        var updated = await _documentRepository.UpdateStatusAsync(
            documentId, DocumentStatus.UnderReview, cancellationToken);

        if (!updated)
            throw new KeyNotFoundException("Document not found during status update.");

        document.Status       = DocumentStatus.UnderReview;
        document.UpdatedAtUtc = DateTime.UtcNow;

        Log.StatusTransitionSuccess(_logger, documentId, DocumentStatus.UnderReview.ToString());
        if (_notifier is not null)
            await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.UnderReview.ToString(), ct: cancellationToken);
        return MapToResponse(document);
    }

    public async Task<DocumentResponse> MarkFailedAsync(
        Guid documentId,
        string? failureReason,
        CancellationToken cancellationToken = default)
    {
        Log.StatusTransitionStart(_logger, documentId, DocumentStatus.Failed.ToString());

        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                       ?? throw new KeyNotFoundException("Document not found.");

        // Idempotent — if already Failed, return current state
        if (document.Status == DocumentStatus.Failed)
        {
            Log.StatusTransitionSkipped(_logger, documentId, document.Status.ToString(), DocumentStatus.Failed.ToString());
            return MapToResponse(document);
        }

        document.Status        = DocumentStatus.Failed;
        document.FailureReason = failureReason;
        document.UpdatedAtUtc  = DateTime.UtcNow;

        try
        {
            await _documentRepository.UpdateAsync(document, cancellationToken);
            Log.StatusTransitionSuccess(_logger, documentId, DocumentStatus.Failed.ToString());
            if (_notifier is not null)
                await _notifier.NotifyStatusChangedAsync(documentId, DocumentStatus.Failed.ToString(), document.FailureReason, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.StatusTransitionDbError(_logger, ex, documentId, DocumentStatus.Failed.ToString());
            throw;
        }

        return MapToResponse(document);
    }

    private static DocumentResponse MapToResponse(LoanDocument document)
    {
        return new DocumentResponse
        {
            Id               = document.Id,
            ApplicationId    = document.ApplicationId,
            UserId           = document.UserId,
            FileName         = document.FileName,
            ContentType      = document.ContentType,
            FileSizeBytes    = document.FileSizeBytes,
            DocumentType     = document.DocumentType,
            Status           = document.Status.ToString(),
            IsVerified       = document.IsVerified,
            VerifiedByUserId = document.VerifiedByUserId,
            VerifiedAtUtc    = document.VerifiedAtUtc,
            Remarks          = document.Remarks,
            FailureReason    = document.FailureReason,
            CreatedAtUtc     = document.CreatedAtUtc,
            UpdatedAtUtc     = document.UpdatedAtUtc
        };
    }

    // =========================================================================
    // LoggerMessage source-generated delegates — zero allocation on hot paths
    // =========================================================================

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "[DocumentService] START — transitioning document {DocumentId} → {TargetStatus}")]
        public static partial void StatusTransitionStart(ILogger logger, Guid documentId, string targetStatus);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "[DocumentService] SUCCESS — document {DocumentId} status → {TargetStatus}")]
        public static partial void StatusTransitionSuccess(ILogger logger, Guid documentId, string targetStatus);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "[DocumentService] SKIPPED — document {DocumentId} already in {CurrentStatus}, " +
                      "transition to {TargetStatus} is a no-op")]
        public static partial void StatusTransitionSkipped(ILogger logger, Guid documentId,
            string currentStatus, string targetStatus);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "[DocumentService] FAILURE — could not persist status {TargetStatus} " +
                      "for document {DocumentId}")]
        public static partial void StatusTransitionDbError(ILogger logger, Exception ex,
            Guid documentId, string targetStatus);
    }
}

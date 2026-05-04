using System.Security.Claims;
using CapFinLoan.Document.Application.Contracts.Requests;
using CapFinLoan.Document.Application.Contracts.Responses;
using CapFinLoan.Document.Application.Interfaces;
using CapFinLoan.Document.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.Document.API.Controllers;

[ApiController]
[Route("api/internal/documents")]
[Authorize(Roles = RoleNames.Admin)]
public class InternalDocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public InternalDocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Internal: Transition a document status.
    /// FluentValidation ensures Status is one of the allowed values before this runs.
    /// ArgumentException / KeyNotFoundException → handled by GlobalExceptionMiddleware.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [AllowAnonymous] // internal service-to-service call — secured by network boundary
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateDocumentStatusRequest request,
        CancellationToken cancellationToken)
    {
        DocumentResponse result = request.Status switch
        {
            "Processing"  => await _documentService.MarkProcessingAsync(id, cancellationToken),
            "Completed"   => await _documentService.MarkCompletedAsync(id, cancellationToken),
            "UnderReview" => await _documentService.MarkUnderReviewAsync(id, cancellationToken),
            "Failed"      => await _documentService.MarkFailedAsync(id, request.FailureReason, cancellationToken),
            _             => throw new ArgumentException($"Status '{request.Status}' is not supported via this endpoint.")
        };

        return Ok(result);
    }

    /// <summary>Admin: Verify or reject a document.</summary>
    [HttpPut("{id:guid}/verify")]
    public async Task<IActionResult> Verify(
        Guid id,
        [FromBody] VerifyDocumentRequest request,
        CancellationToken cancellationToken)
    {
        // KeyNotFoundException → 404 via GlobalExceptionMiddleware
        var result = await _documentService.VerifyAsync(id, GetUserId(), request.IsVerified, request.Remarks, cancellationToken);
        return Ok(result);
    }

    /// <summary>Admin: Get all documents for a specific application.</summary>
    [HttpGet("application/{applicationId:guid}")]
    public async Task<IActionResult> GetByApplicationId(Guid applicationId, CancellationToken cancellationToken)
    {
        var documents = await _documentService.GetByApplicationIdAsync(applicationId, cancellationToken);
        return Ok(documents);
    }

    /// <summary>Admin: Download a document file by ID.</summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _documentService.GetByIdAsync(id, cancellationToken);
        var stream = await _documentService.GetFileStreamAsync(doc.Id, cancellationToken);
        if (stream == null) return NotFound(new { message = "File not found on disk." });
        return File(stream, doc.ContentType, doc.FileName);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }
}

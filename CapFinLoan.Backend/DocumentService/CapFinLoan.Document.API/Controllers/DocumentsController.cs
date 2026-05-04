using System.Security.Claims;
using CapFinLoan.Document.Application.Interfaces;
using CapFinLoan.Document.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.Document.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>Upload a document for a loan application.</summary>
    [HttpPost("upload")]
    [Authorize(Roles = RoleNames.Applicant)]
    public async Task<IActionResult> Upload(
        [FromForm] Guid applicationId,
        [FromForm] string documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        // Validation exceptions (InvalidOperationException) are caught by GlobalExceptionMiddleware → 400
        var result = await _documentService.UploadAsync(GetUserId(), applicationId, documentType, file, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get all documents for a specific application.</summary>
    [HttpGet("application/{applicationId:guid}")]
    public async Task<IActionResult> GetByApplicationId(Guid applicationId, CancellationToken cancellationToken)
    {
        var documents = await _documentService.GetByApplicationIdAsync(applicationId, cancellationToken);
        return Ok(documents);
    }

    /// <summary>Get all documents uploaded by the current user.</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var documents = await _documentService.GetByUserIdAsync(GetUserId(), cancellationToken);
        return Ok(documents);
    }

    /// <summary>Get all documents uploaded by a specific user (dashboard).</summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        var documents = await _documentService.GetByUserIdAsync(userId, cancellationToken);
        return Ok(documents);
    }

    /// <summary>Get a single document's metadata by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        // KeyNotFoundException → 404 via GlobalExceptionMiddleware
        var document = await _documentService.GetByIdAsync(id, cancellationToken);
        return Ok(document);
    }

    /// <summary>Replace/edit a previously uploaded document.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Applicant)]
    public async Task<IActionResult> Replace(
        Guid id,
        IFormFile file,
        [FromForm] string? documentType,
        CancellationToken cancellationToken)
    {
        // KeyNotFoundException → 404, UnauthorizedAccessException → 403,
        // InvalidOperationException → 400 — all via GlobalExceptionMiddleware
        var result = await _documentService.ReplaceAsync(GetUserId(), id, file, documentType, cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }
}

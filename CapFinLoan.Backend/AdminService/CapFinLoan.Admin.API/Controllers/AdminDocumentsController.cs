using CapFinLoan.Admin.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace CapFinLoan.Admin.API.Controllers;

[ApiController]
[Route("api/admin/documents")]
[Authorize(Roles = RoleNames.Admin)]
public class AdminDocumentsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdminDocumentsController> _logger;

    public AdminDocumentsController(IHttpClientFactory httpClientFactory, ILogger<AdminDocumentsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private HttpClient CreateInternalClient()
    {
        var client = _httpClientFactory.CreateClient("DocumentServiceClient");
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken cancellationToken)
    {
        var client = CreateInternalClient();
        var response = await client.GetAsync($"/api/internal/documents/{id}/download", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                        ?? response.Content.Headers.ContentDisposition?.FileName
                        ?? "document";
            return File(stream, contentType, fileName);
        }

        return StatusCode((int)response.StatusCode);
    }

    [HttpGet("application/{applicationId:guid}")]
    public async Task<IActionResult> GetByApplicationId(Guid applicationId, CancellationToken cancellationToken)
    {
        var client = CreateInternalClient();
        var response = await client.GetAsync($"/api/internal/documents/application/{applicationId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return Content(content, "application/json");
        }

        _logger.LogError("Failed to fetch documents from Document Service for application {ApplicationId}. Status: {StatusCode}", applicationId, response.StatusCode);
        return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [HttpPut("{id:guid}/verify")]
    public async Task<IActionResult> VerifyDocument(Guid id, [FromBody] object request, CancellationToken cancellationToken)
    {
        var client = CreateInternalClient();
        var response = await client.PutAsJsonAsync($"/api/internal/documents/{id}/verify", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return Content(content, "application/json");
        }

        _logger.LogError("Failed to verify document in Document Service. Status: {StatusCode}", response.StatusCode);
        return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }
}

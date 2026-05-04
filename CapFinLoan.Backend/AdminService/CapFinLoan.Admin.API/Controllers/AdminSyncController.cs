using System.Net.Http.Headers;
using System.Text.Json;
using CapFinLoan.Admin.Domain.Constants;
using CapFinLoan.Admin.Domain.Entities;
using CapFinLoan.Admin.Persistence.Data;
using CapFinLoan.Api.Shared.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Admin.API.Controllers;

/// <summary>
/// One-shot sync endpoint — pulls all non-Draft applications from ApplicationService
/// and upserts them into the admin DB.
/// Fixes the gap caused by the ApplicationSubmittedHandler previously only logging.
/// </summary>
[ApiController]
[Route("api/admin/sync")]
[Authorize(Roles = RoleNames.Admin)]
public class AdminSyncController : ControllerBase
{
    private readonly AdminDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICacheService _cache;
    private readonly ILogger<AdminSyncController> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AdminSyncController(
        AdminDbContext db,
        IHttpClientFactory httpClientFactory,
        ICacheService cache,
        ILogger<AdminSyncController> logger)
    {
        _db                = db;
        _httpClientFactory = httpClientFactory;
        _cache             = cache;
        _logger            = logger;
    }

    /// <summary>
    /// POST /api/admin/sync/applications
    /// Fetches all non-Draft applications from ApplicationService and upserts into admin DB.
    /// Returns counts of inserted and updated records.
    /// </summary>
    [HttpPost("applications")]
    public async Task<IActionResult> SyncApplications(CancellationToken ct)
    {
        // ── 1. Fetch from ApplicationService ─────────────────────────────────
        var client = _httpClientFactory.CreateClient("ApplicationServiceClient");

        // Forward the caller's JWT so the internal endpoint can validate it
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/internal/applications/all");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sync] Failed to reach ApplicationService");
            return StatusCode(502, new { message = "Could not reach ApplicationService." });
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[Sync] ApplicationService returned {Status}: {Body}", response.StatusCode, body);
            return StatusCode((int)response.StatusCode, new { message = "ApplicationService error during sync." });
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var sourceApps = JsonSerializer.Deserialize<List<SourceApplication>>(json, _json) ?? [];

        // ── 2. Upsert into admin DB ───────────────────────────────────────────
        int inserted = 0, updated = 0;

        foreach (var src in sourceApps)
        {
            var existing = await _db.LoanApplications
                .FirstOrDefaultAsync(x => x.Id == src.Id, ct);

            if (existing is null)
            {
                var app = new LoanApplication
                {
                    Id                    = src.Id,
                    ApplicantUserId       = src.ApplicantUserId,
                    ApplicationNumber     = src.ApplicationNumber,
                    Status                = src.Status,
                    FirstName             = src.FirstName ?? string.Empty,
                    LastName              = src.LastName  ?? string.Empty,
                    Email                 = src.Email     ?? string.Empty,
                    Phone                 = src.Phone     ?? string.Empty,
                    AddressLine1          = src.AddressLine1  ?? string.Empty,
                    AddressLine2          = src.AddressLine2  ?? string.Empty,
                    City                  = src.City          ?? string.Empty,
                    State                 = src.State         ?? string.Empty,
                    PostalCode            = src.PostalCode    ?? string.Empty,
                    EmployerName          = src.EmployerName  ?? string.Empty,
                    EmploymentType        = src.EmploymentType ?? string.Empty,
                    MonthlyIncome         = src.MonthlyIncome,
                    AnnualIncome          = src.AnnualIncome,
                    ExistingEmiAmount     = src.ExistingEmiAmount,
                    RequestedAmount       = src.RequestedAmount,
                    RequestedTenureMonths = src.RequestedTenureMonths,
                    LoanPurpose           = src.LoanPurpose ?? string.Empty,
                    Remarks               = src.Remarks    ?? string.Empty,
                    CreatedAtUtc          = src.CreatedAtUtc,
                    UpdatedAtUtc          = src.UpdatedAtUtc,
                    SubmittedAtUtc        = src.SubmittedAtUtc
                };

                // Add initial status history entry
                if (src.SubmittedAtUtc.HasValue)
                {
                    app.StatusHistory.Add(new ApplicationStatusHistory
                    {
                        LoanApplicationId = app.Id,
                        FromStatus        = ApplicationStatuses.Draft,
                        ToStatus          = src.Status,
                        Remarks           = "Synced from ApplicationService.",
                        ChangedByUserId   = src.ApplicantUserId,
                        ChangedAtUtc      = src.SubmittedAtUtc.Value
                    });
                }

                _db.LoanApplications.Add(app);
                inserted++;
            }
            else
            {
                // Update status and key fields if they've changed
                if (!string.Equals(existing.Status, src.Status, StringComparison.OrdinalIgnoreCase)
                    || existing.UpdatedAtUtc < src.UpdatedAtUtc)
                {
                    existing.Status               = src.Status;
                    existing.UpdatedAtUtc         = src.UpdatedAtUtc;
                    existing.SubmittedAtUtc       = src.SubmittedAtUtc;
                    existing.RequestedAmount      = src.RequestedAmount;
                    existing.RequestedTenureMonths = src.RequestedTenureMonths;
                    // Update name/contact in case they were edited
                    existing.FirstName    = src.FirstName    ?? existing.FirstName;
                    existing.LastName     = src.LastName     ?? existing.LastName;
                    existing.Email        = src.Email        ?? existing.Email;
                    existing.Phone        = src.Phone        ?? existing.Phone;
                    existing.LoanPurpose  = src.LoanPurpose  ?? existing.LoanPurpose;
                    updated++;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        // ── 3. Bust caches ────────────────────────────────────────────────────
        await _cache.RemoveByPrefixAsync("admin:queue:", ct);
        await _cache.RemoveAsync("admin:dashboard", ct);

        _logger.LogInformation("[Sync] Completed — inserted: {Inserted}, updated: {Updated}", inserted, updated);

        return Ok(new
        {
            message  = "Sync completed successfully.",
            inserted,
            updated,
            total    = sourceApps.Count
        });
    }

    // ── DTO for deserialising ApplicationService response ─────────────────────
    private sealed class SourceApplication
    {
        public Guid     Id                    { get; set; }
        public Guid     ApplicantUserId       { get; set; }
        public string   ApplicationNumber     { get; set; } = string.Empty;
        public string   Status                { get; set; } = string.Empty;
        public string?  FirstName             { get; set; }
        public string?  LastName              { get; set; }
        public string?  Email                 { get; set; }
        public string?  Phone                 { get; set; }
        public string?  AddressLine1          { get; set; }
        public string?  AddressLine2          { get; set; }
        public string?  City                  { get; set; }
        public string?  State                 { get; set; }
        public string?  PostalCode            { get; set; }
        public string?  EmployerName          { get; set; }
        public string?  EmploymentType        { get; set; }
        public decimal? MonthlyIncome         { get; set; }
        public decimal? AnnualIncome          { get; set; }
        public decimal  ExistingEmiAmount     { get; set; }
        public decimal  RequestedAmount       { get; set; }
        public int      RequestedTenureMonths { get; set; }
        public string?  LoanPurpose           { get; set; }
        public string?  Remarks               { get; set; }
        public DateTime CreatedAtUtc          { get; set; }
        public DateTime UpdatedAtUtc          { get; set; }
        public DateTime? SubmittedAtUtc       { get; set; }
    }
}

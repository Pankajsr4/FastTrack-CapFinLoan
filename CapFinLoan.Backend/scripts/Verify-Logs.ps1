#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verifies that all expected log entries appear in the Serilog rolling log files
    for AdminService and DocumentService after a document upload.

.DESCRIPTION
    Reads the current day's log files and checks for the presence of every
    expected log message across the full processing pipeline:

      AdminService  logs/admin-service-<date>.log
      DocumentService  logs/document-service-<date>.log

    Checks are grouped into four categories:
      1. Message received
      2. Processing started / stage transitions
      3. Processing completed
      4. Errors (failure path)

.USAGE
    # Run from repo root after uploading at least one document:
    pwsh CapFinLoan.Backend/scripts/Verify-Logs.ps1

    # Tail live logs instead of scanning files:
    pwsh CapFinLoan.Backend/scripts/Verify-Logs.ps1 -TailMode

    # Filter to a specific DocumentId:
    pwsh CapFinLoan.Backend/scripts/Verify-Logs.ps1 -DocumentId "7c9e6679-7425-40de-944b-e07fc1f90ae7"

    # Point to custom log directories:
    pwsh CapFinLoan.Backend/scripts/Verify-Logs.ps1 `
        -AdminLogDir  "CapFinLoan.Backend/AdminService/CapFinLoan.Admin.API/logs" `
        -DocLogDir    "CapFinLoan.Backend/DocumentService/CapFinLoan.Document.API/logs"
#>

param(
    [string] $AdminLogDir = "CapFinLoan.Backend/AdminService/CapFinLoan.Admin.API/logs",
    [string] $DocLogDir   = "CapFinLoan.Backend/DocumentService/CapFinLoan.Document.API/logs",
    [string] $DocumentId  = "",          # optional: filter checks to a specific document
    [switch] $TailMode,                  # stream live logs instead of scanning files
    [int]    $TailLines   = 200          # how many recent lines to scan in tail mode
)

# ── Helpers ───────────────────────────────────────────────────────────────────

$pass = 0; $fail = 0; $warn = 0

function Section($title) {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
    Write-Host "  $title" -ForegroundColor White
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
}

function Ok($msg, $sample = "") {
    Write-Host "  ✓ $msg" -ForegroundColor Green
    if ($sample) { Write-Host "    $sample" -ForegroundColor DarkGray }
    $script:pass++
}

function Fail($msg, $hint = "") {
    Write-Host "  ✗ $msg" -ForegroundColor Red
    if ($hint) { Write-Host "    Hint: $hint" -ForegroundColor Yellow }
    $script:fail++
}

function Warn($msg, $hint = "") {
    Write-Host "  ⚠ $msg" -ForegroundColor Yellow
    if ($hint) { Write-Host "    $hint" -ForegroundColor DarkGray }
    $script:warn++
}

function Info($msg) { Write-Host "  ℹ $msg" -ForegroundColor Cyan }

# Load today's log file for a service. Returns array of lines.
function Get-LogLines($logDir, $prefix) {
    $today = (Get-Date).ToString("yyyyMMdd")
    # Serilog rolling file: logs/admin-service-20260404.log
    $pattern = Join-Path $logDir "$prefix$today.log"
    if (Test-Path $pattern) {
        return [System.IO.File]::ReadAllLines($pattern)
    }
    # Fallback: most recently modified log file in the directory
    $latest = Get-ChildItem $logDir -Filter "*.log" -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        Warn "Today's log not found — using most recent: $($latest.Name)"
        return [System.IO.File]::ReadAllLines($latest.FullName)
    }
    return @()
}

# Check whether $lines contains at least one line matching $pattern.
# $pattern is a simple wildcard (* supported). Returns the first matching line.
function Find-LogEntry($lines, [string]$pattern, [string]$docId = "") {
    $filtered = if ($docId) { $lines | Where-Object { $_ -like "*$docId*" } } else { $lines }
    $match    = $filtered | Where-Object { $_ -like $pattern } | Select-Object -First 1
    return $match
}

# Assert a log entry exists; print pass/fail with the matched line as sample.
function Assert-Log($lines, [string]$pattern, [string]$description, [string]$hint = "", [string]$docId = "") {
    $match = Find-LogEntry $lines $pattern $docId
    if ($match) {
        # Trim the line to 120 chars for display
        $sample = $match.Trim()
        if ($sample.Length -gt 120) { $sample = $sample.Substring(0, 117) + "..." }
        Ok $description $sample
    } else {
        Fail $description $hint
    }
}

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     CapFinLoan — Logging Verification                        ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Info "AdminService log dir:    $AdminLogDir"
Info "DocumentService log dir: $DocLogDir"
if ($DocumentId) { Info "Filtering to DocumentId: $DocumentId" }

# ── Load log files ────────────────────────────────────────────────────────────

$adminLines = Get-LogLines $AdminLogDir    "admin-service-"
$docLines   = Get-LogLines $DocLogDir      "document-service-"

if ($adminLines.Count -eq 0) {
    Write-Host ""
    Write-Host "  ✗ No AdminService log lines found in: $AdminLogDir" -ForegroundColor Red
    Write-Host "    Start AdminService and upload a document first." -ForegroundColor Yellow
    Write-Host "    Expected file: $AdminLogDir/admin-service-$(Get-Date -Format 'yyyyMMdd').log" -ForegroundColor DarkGray
}

if ($docLines.Count -eq 0) {
    Write-Host ""
    Write-Host "  ✗ No DocumentService log lines found in: $DocLogDir" -ForegroundColor Red
    Write-Host "    Start DocumentService and upload a document first." -ForegroundColor Yellow
}

Info "AdminService log lines loaded:    $($adminLines.Count)"
Info "DocumentService log lines loaded: $($docLines.Count)"

# =============================================================================
# CATEGORY 1 — Message Received
# Service: AdminService  |  Source: RabbitMqConsumer<T> + DocumentUploadedHandler
# =============================================================================

Section "1. Message Received  [AdminService]"

Assert-Log $adminLines `
    "*[DocumentUploadedEvent]*Listening on queue*document-uploaded-event*" `
    "Consumer started and listening on queue" `
    "Check AdminService startup — RabbitMqConsumer<DocumentUploadedEvent>.ExecuteAsync" `
    $DocumentId

Assert-Log $adminLines `
    "*[DocumentUploadedEvent]*Message received*delivery*" `
    "RabbitMqConsumer: message received (delivery tag logged)" `
    "Source: RabbitMqConsumer<T>.HandleDeliveryAsync — Log.MessageReceived" `
    $DocumentId

Assert-Log $adminLines `
    "*[DocumentUploadedHandler]*Message received*DocumentId*" `
    "DocumentUploadedHandler: message received with all fields" `
    "Source: DocumentUploadedHandler.HandleAsync — first log statement" `
    $DocumentId

Assert-Log $adminLines `
    "*[DocumentUploadedEvent]*Handler started*DocumentUploadedHandler*" `
    "RabbitMqConsumer: handler dispatched (handler type logged)" `
    "Source: RabbitMqConsumer<T>.DispatchInScopeAsync — Log.HandlerStarted" `
    $DocumentId

# =============================================================================
# CATEGORY 2 — Processing Started / Stage Transitions
# Service: AdminService  |  Source: DocumentProcessingService
# =============================================================================

Section "2. Processing Started & Stage Transitions  [AdminService]"

Assert-Log $adminLines `
    "*Processing started*DocumentId*ApplicationId*UserId*" `
    "Processing pipeline started (all event fields logged)" `
    "Source: DocumentProcessingService.ProcessDocumentAsync — Log.ProcessingStart" `
    $DocumentId

Assert-Log $adminLines `
    "*Processing record created*DocumentId*RecordId*" `
    "AdminService DB record created (DocumentProcessingRecord inserted)" `
    "Source: DocumentProcessingService — Log.RecordCreated" `
    $DocumentId

Assert-Log $adminLines `
    "*Processing stage set*DocumentId*Validating*" `
    "Stage transition: → Validating" `
    "Source: DocumentProcessingService.SetStageAsync — Log.StageSet" `
    $DocumentId

Assert-Log $adminLines `
    "*Processing stage complete*DocumentId*Validating*" `
    "Stage complete: Validating ✓" `
    "Source: DocumentProcessingService — Log.StageComplete" `
    $DocumentId

Assert-Log $adminLines `
    "*Processing stage set*DocumentId*Processing*" `
    "Stage transition: → Processing" `
    "Source: DocumentProcessingService.SetStageAsync" `
    $DocumentId

Assert-Log $adminLines `
    "*Processing stage complete*DocumentId*Processing*" `
    "Stage complete: Processing ✓" `
    "Source: DocumentProcessingService — Log.StageComplete" `
    $DocumentId

# DocumentService receives the HTTP PATCH calls — verify it logs the transitions
Section "2b. Status Transitions  [DocumentService]"

Assert-Log $docLines `
    "*[DocumentService]*START*transitioning document*Processing*" `
    "DocumentService: START — transitioning → Processing" `
    "Source: DocumentService.MarkProcessingAsync — Log.StatusTransitionStart" `
    $DocumentId

Assert-Log $docLines `
    "*[DocumentService]*SUCCESS*document*Processing*" `
    "DocumentService: SUCCESS — status → Processing persisted" `
    "Source: DocumentService.MarkProcessingAsync — Log.StatusTransitionSuccess" `
    $DocumentId

Assert-Log $docLines `
    "*[DocumentService]*START*transitioning document*Completed*" `
    "DocumentService: START — transitioning → Completed" `
    "Source: DocumentService.MarkCompletedAsync" `
    $DocumentId

Assert-Log $docLines `
    "*[DocumentService]*SUCCESS*document*Completed*" `
    "DocumentService: SUCCESS — status → Completed persisted" `
    "Source: DocumentService.MarkCompletedAsync" `
    $DocumentId

Assert-Log $docLines `
    "*[DocumentService]*START*transitioning document*UnderReview*" `
    "DocumentService: START — transitioning → UnderReview" `
    "Source: DocumentService.MarkUnderReviewAsync" `
    $DocumentId

Assert-Log $docLines `
    "*[DocumentService]*SUCCESS*document*UnderReview*" `
    "DocumentService: SUCCESS — status → UnderReview persisted" `
    "Source: DocumentService.MarkUnderReviewAsync" `
    $DocumentId

# =============================================================================
# CATEGORY 3 — Processing Completed
# Service: AdminService  |  Source: DocumentProcessingService + RabbitMqConsumer
# =============================================================================

Section "3. Processing Completed  [AdminService]"

Assert-Log $adminLines `
    "*Processing completed*DocumentId*queued for review*RecordId*" `
    "Pipeline completed — document queued for admin review (elapsed ms logged)" `
    "Source: DocumentProcessingService — Log.ProcessingSuccess" `
    $DocumentId

Assert-Log $adminLines `
    "*[DocumentUploadedHandler]*Processing result: SUCCESS*DocumentId*Ack*" `
    "Handler result: SUCCESS → Ack" `
    "Source: DocumentUploadedHandler.HandleAsync — success branch" `
    $DocumentId

Assert-Log $adminLines `
    "*[DocumentUploadedEvent]*Message processed*delivery*" `
    "RabbitMqConsumer: message acked (elapsed ms logged)" `
    "Source: RabbitMqConsumer<T>.DispatchInScopeAsync — Log.MessageProcessed" `
    $DocumentId

# HTTP request log from Serilog request middleware (DocumentService side)
Section "3b. HTTP Request Logs  [DocumentService]"

Assert-Log $docLines `
    "*PATCH*internal/documents*status*200*" `
    "DocumentService: PATCH /api/internal/documents/{id}/status → 200 OK" `
    "Source: Serilog request logging middleware (UseSerilogRequestLogging)" `
    $DocumentId

# =============================================================================
# CATEGORY 4 — Errors (failure path)
# =============================================================================

Section "4. Error Logs  [AdminService — failure path]"

# These will only appear if a failure scenario was run.
# We check for presence and warn (not fail) if absent — normal on a clean run.

$malformedMatch = Find-LogEntry $adminLines "*Deserialization failed*delivery*" $DocumentId
if ($malformedMatch) {
    Ok "Deserialization failure logged (malformed JSON scenario)" `
       $malformedMatch.Trim().Substring(0, [Math]::Min(120, $malformedMatch.Trim().Length))
} else {
    Warn "No deserialization failure log found" `
         "Expected after running failure scenario F1 (malformed JSON)"
}

$validationMatch = Find-LogEntry $adminLines "*Processing failed (validation)*DocumentId*" $DocumentId
if ($validationMatch) {
    Ok "Validation failure logged (ArgumentException / InvalidOperationException)" `
       $validationMatch.Trim().Substring(0, [Math]::Min(120, $validationMatch.Trim().Length))
} else {
    Warn "No validation failure log found" `
         "Expected after running failure scenarios F2/F3/F4 (bad fields / content type / size)"
}

$permanentMatch = Find-LogEntry $adminLines "*PERMANENT FAILURE*NackDiscard*DLQ*" $DocumentId
if ($permanentMatch) {
    Ok "Permanent failure logged → NackDiscard (DLQ)" `
       $permanentMatch.Trim().Substring(0, [Math]::Min(120, $permanentMatch.Trim().Length))
} else {
    Warn "No permanent failure log found" `
         "Expected after running any permanent failure scenario"
}

$transientMatch = Find-LogEntry $adminLines "*TRANSIENT FAILURE*NackRequeue*retry*" $DocumentId
if ($transientMatch) {
    Ok "Transient failure logged → NackRequeue (retry)" `
       $transientMatch.Trim().Substring(0, [Math]::Min(120, $transientMatch.Trim().Length))
} else {
    Warn "No transient failure log found" `
         "Expected after running failure scenario F6 (non-existent DocumentId)"
}

$retryMatch = Find-LogEntry $adminLines "*Retry attempt*x-retry-count*" $DocumentId
if ($retryMatch) {
    Ok "Retry attempt logged (x-retry-count incremented)" `
       $retryMatch.Trim().Substring(0, [Math]::Min(120, $retryMatch.Trim().Length))
} else {
    Warn "No retry attempt log found" `
         "Expected after transient failure scenario — shows x-retry-count progression"
}

$maxRetryMatch = Find-LogEntry $adminLines "*Max retries exceeded*dead-lettering*" $DocumentId
if ($maxRetryMatch) {
    Ok "Max retries exceeded logged → dead-lettering to DLQ" `
       $maxRetryMatch.Trim().Substring(0, [Math]::Min(120, $maxRetryMatch.Trim().Length))
} else {
    Warn "No max-retries-exceeded log found" `
         "Expected after retry exhaustion scenario (x-retry-count >= MaxDeliveryCount)"
}

$processingFailedMatch = Find-LogEntry $adminLines "*Processing failed*DocumentId*ExceptionType*" $DocumentId
if ($processingFailedMatch) {
    Ok "Processing pipeline failure logged (exception type + message + elapsed)" `
       $processingFailedMatch.Trim().Substring(0, [Math]::Min(120, $processingFailedMatch.Trim().Length))
} else {
    Warn "No processing pipeline failure log found" `
         "Expected after any transient failure that reaches DocumentProcessingService"
}

$failedDocMatch = Find-LogEntry $docLines "*[DocumentService]*FAILURE*could not persist status*Failed*" $DocumentId
if ($failedDocMatch) {
    Ok "DocumentService: DB error on Failed status persisted (logged)" `
       $failedDocMatch.Trim().Substring(0, [Math]::Min(120, $failedDocMatch.Trim().Length))
} else {
    Warn "No DocumentService DB failure log found" `
         "Only appears when MarkFailedAsync itself throws — rare"
}

# =============================================================================
# CATEGORY 5 — Infrastructure / Startup logs
# =============================================================================

Section "5. Infrastructure & Startup  [AdminService]"

Assert-Log $adminLines `
    "*AdminService starting up*environment*" `
    "AdminService startup log" `
    "Source: Program.cs — app.Logger.LogInformation" `
    ""

Assert-Log $adminLines `
    "*[DocumentUploadedEvent]*Starting*queue*document-uploaded-event*prefetch*" `
    "RabbitMqConsumer started (queue + prefetch + maxDelivery logged)" `
    "Source: RabbitMqConsumer<T>.ExecuteAsync" `
    ""

Assert-Log $adminLines `
    "*Topology declared*document-uploaded-event*dlx*document-uploaded-event.dlx*dlq*" `
    "Queue topology declared (main queue + DLX + DLQ)" `
    "Source: RabbitMqConsumer<T>.DeclareTopologyAsync" `
    ""

Section "5b. Infrastructure & Startup  [DocumentService]"

Assert-Log $docLines `
    "*Document Service starting up*environment*" `
    "DocumentService startup log" `
    "Source: Program.cs — app.Logger.LogInformation" `
    ""

# =============================================================================
# CATEGORY 6 — Tail mode: stream live logs
# =============================================================================

if ($TailMode) {
    Section "6. Live Log Tail (Ctrl+C to stop)"

    $adminLog = Get-ChildItem $AdminLogDir -Filter "*.log" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $docLog   = Get-ChildItem $DocLogDir   -Filter "*.log" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $adminLog -and -not $docLog) {
        Warn "No log files found for tail mode"
    } else {
        Info "Tailing logs — upload a document to see live entries..."
        Info "AdminService:    $(if ($adminLog) { $adminLog.FullName } else { 'not found' })"
        Info "DocumentService: $(if ($docLog)   { $docLog.FullName   } else { 'not found' })"
        Write-Host ""

        # Colour-code by level
        function Write-LogLine($line, $service) {
            $colour = switch -Wildcard ($line) {
                "*[ERR]*" { "Red"     }
                "*[WRN]*" { "Yellow"  }
                "*[INF]*" { "White"   }
                "*[DBG]*" { "DarkGray"}
                default   { "Gray"    }
            }
            $prefix = if ($service -eq "Admin") { "[ADM]" } else { "[DOC]" }
            Write-Host "$prefix $line" -ForegroundColor $colour
        }

        # Print last $TailLines from each file, then follow
        if ($adminLog) {
            Get-Content $adminLog.FullName -Tail $TailLines |
                ForEach-Object { Write-LogLine $_ "Admin" }
        }
        if ($docLog) {
            Get-Content $docLog.FullName -Tail $TailLines |
                ForEach-Object { Write-LogLine $_ "Doc" }
        }

        # Follow both files concurrently using background jobs
        $jobs = @()
        if ($adminLog) {
            $jobs += Start-Job -ScriptBlock {
                param($path)
                Get-Content $path -Wait -Tail 0 | ForEach-Object { "[ADM] $_" }
            } -ArgumentList $adminLog.FullName
        }
        if ($docLog) {
            $jobs += Start-Job -ScriptBlock {
                param($path)
                Get-Content $path -Wait -Tail 0 | ForEach-Object { "[DOC] $_" }
            } -ArgumentList $docLog.FullName
        }

        try {
            while ($true) {
                foreach ($job in $jobs) {
                    $output = Receive-Job $job
                    foreach ($line in $output) {
                        $colour = switch -Wildcard ($line) {
                            "*[ERR]*" { "Red"     }
                            "*[WRN]*" { "Yellow"  }
                            "*[INF]*" { "White"   }
                            "*[DBG]*" { "DarkGray"}
                            default   { "Gray"    }
                        }
                        Write-Host $line -ForegroundColor $colour
                    }
                }
                Start-Sleep -Milliseconds 300
            }
        } finally {
            $jobs | Stop-Job
            $jobs | Remove-Job
        }
    }
}

# =============================================================================
# Summary
# =============================================================================

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    LOG VERIFICATION SUMMARY                 ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$total = $pass + $fail + $warn
Write-Host "  Total checks : $total"
Write-Host "  Found        : $pass" -ForegroundColor Green
if ($warn -gt 0) { Write-Host "  Warnings     : $warn  (failure-path logs — only present after failure scenarios)" -ForegroundColor Yellow }
if ($fail -gt 0) { Write-Host "  Missing      : $fail" -ForegroundColor Red }

Write-Host ""

if ($fail -eq 0 -and $warn -eq 0) {
    Write-Host "  ✓ All expected log entries found!" -ForegroundColor Green
} elseif ($fail -eq 0) {
    Write-Host "  ✓ Happy-path logs complete. Run failure scenarios to populate error logs." -ForegroundColor Green
    Write-Host "    pwsh CapFinLoan.Backend/scripts/Test-FailureScenarios.ps1" -ForegroundColor DarkGray
} else {
    Write-Host "  ✗ Some expected log entries are missing." -ForegroundColor Red
    Write-Host ""
    Write-Host "  Troubleshooting:" -ForegroundColor Yellow
    Write-Host "    1. Ensure services are running and a document was uploaded"
    Write-Host "    2. Check log directory paths:"
    Write-Host "       AdminService:    $AdminLogDir"
    Write-Host "       DocumentService: $DocLogDir"
    Write-Host "    3. Verify serilog.json MinimumLevel.Default = Information"
    Write-Host "    4. For Debug-level entries, set CapFinLoan.Messaging override to Debug"
    Write-Host "    5. Use tail mode to watch live: -TailMode"
}

Write-Host ""
Write-Host "  Log file locations:" -ForegroundColor DarkGray
Write-Host "    AdminService:    $AdminLogDir/admin-service-$(Get-Date -Format 'yyyyMMdd').log" -ForegroundColor DarkGray
Write-Host "    DocumentService: $DocLogDir/document-service-$(Get-Date -Format 'yyyyMMdd').log" -ForegroundColor DarkGray
Write-Host ""

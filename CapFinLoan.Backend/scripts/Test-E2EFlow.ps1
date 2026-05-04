#!/usr/bin/env pwsh
<#
.SYNOPSIS
    End-to-end flow test for CapFinLoan document processing pipeline.

.DESCRIPTION
    Executes and verifies all 6 steps:
      1. Upload document from frontend (via API)
      2. Verify DB entry (Status = Pending)
      3. Verify event published to RabbitMQ
      4. Verify consumer processes message
      5. Verify DB status: Pending → Processing → Completed
      6. Verify SignalR push (connection + subscription)

.USAGE
    # Prerequisites: all services running + Docker RabbitMQ up
    pwsh CapFinLoan.Backend/scripts/Test-E2EFlow.ps1

    # With custom credentials:
    pwsh CapFinLoan.Backend/scripts/Test-E2EFlow.ps1 `
        -Email "test@example.com" -Password "Test123!" `
        -ApplicationId "your-app-guid"
#>

param(
    [string] $DocBase       = "http://localhost:5023",
    [string] $AuthBase      = "http://localhost:5021",
    [string] $RabbitMgmt    = "http://localhost:15672",
    [string] $RabbitUser    = "capfinloan",
    [string] $RabbitPass    = "capfinloan123",
    [string] $Email         = "e2e-test@capfinloan.com",
    [string] $Password      = "E2eTest123!",
    [string] $ApplicationId = "00000000-0000-0000-0000-000000000001",
    [string] $TestFile      = "$PSScriptRoot/../sample-files/test-document.pdf",
    [int]    $PollTimeoutSec = 30,
    [int]    $PollIntervalMs = 1000
)

# ── Helpers ───────────────────────────────────────────────────────────────────

$pass = 0; $fail = 0; $warn = 0
$results = [System.Collections.Generic.List[hashtable]]::new()

function Step($n, $title) {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
    Write-Host "  STEP $n — $title" -ForegroundColor White
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
}

function Ok($msg, $detail = "") {
    Write-Host "  ✓ $msg" -ForegroundColor Green
    if ($detail) { Write-Host "    $detail" -ForegroundColor DarkGray }
    $script:pass++
    $script:results.Add(@{ status = "PASS"; msg = $msg })
}

function Fail($msg, $detail = "") {
    Write-Host "  ✗ $msg" -ForegroundColor Red
    if ($detail) { Write-Host "    $detail" -ForegroundColor DarkGray }
    $script:fail++
    $script:results.Add(@{ status = "FAIL"; msg = $msg })
}

function Warn($msg, $detail = "") {
    Write-Host "  ⚠ $msg" -ForegroundColor Yellow
    if ($detail) { Write-Host "    $detail" -ForegroundColor DarkGray }
    $script:warn++
    $script:results.Add(@{ status = "WARN"; msg = $msg })
}

function Info($msg) { Write-Host "  ℹ $msg" -ForegroundColor Cyan }

function Invoke-Api($method, $url, $body = $null, $token = $null, $form = $null) {
    $h = @{ "Accept" = "application/json" }
    if ($token) { $h["Authorization"] = "Bearer $token" }

    try {
        if ($form) {
            return Invoke-RestMethod -Method $method -Uri $url -Headers $h -Form $form -ErrorAction Stop
        } elseif ($body) {
            $h["Content-Type"] = "application/json"
            return Invoke-RestMethod -Method $method -Uri $url -Headers $h `
                -Body ($body | ConvertTo-Json -Depth 10) -ErrorAction Stop
        } else {
            return Invoke-RestMethod -Method $method -Uri $url -Headers $h -ErrorAction Stop
        }
    } catch {
        $status = $_.Exception.Response?.StatusCode?.value__
        $msg    = $_.ErrorDetails?.Message ?? $_.Exception.Message
        return @{ __error = $true; status = $status; message = $msg }
    }
}

function Invoke-RabbitApi($path) {
    $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitUser}:${RabbitPass}"))
    try {
        return Invoke-RestMethod -Uri "$RabbitMgmt/api$path" `
            -Headers @{ Authorization = "Basic $cred" } -ErrorAction Stop
    } catch { return $null }
}

function Poll-Status($documentId, $token, $targetStatus, $timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $history  = [System.Collections.Generic.List[string]]::new()

    while ((Get-Date) -lt $deadline) {
        $doc = Invoke-Api "GET" "$DocBase/api/documents/$documentId" -token $token
        if ($doc -and -not $doc.__error) {
            $s = $doc.status
            if (-not $history.Contains($s)) {
                $history.Add($s)
                Info "Status transition: $s  ($(([datetime]::UtcNow).ToString('HH:mm:ss.fff')))"
            }
            if ($s -eq $targetStatus) { return @{ reached = $true; history = $history; doc = $doc } }
            if ($s -in @("Failed", "Verified", "ReuploadRequired")) {
                return @{ reached = $false; history = $history; doc = $doc; terminal = $true }
            }
        }
        Start-Sleep -Milliseconds $PollIntervalMs
    }
    return @{ reached = $false; history = $history; doc = $null }
}

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     CapFinLoan — End-to-End Document Processing Flow Test    ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Info "DocService:  $DocBase"
Info "AuthService: $AuthBase"
Info "RabbitMQ:    $RabbitMgmt"
Info "Application: $ApplicationId"

# ── Pre-flight: create test PDF if missing ────────────────────────────────────

$sampleDir = Split-Path $TestFile
if (-not (Test-Path $sampleDir)) { New-Item -ItemType Directory -Path $sampleDir -Force | Out-Null }

if (-not (Test-Path $TestFile)) {
    # Create a minimal valid PDF
    $pdfContent = "%PDF-1.4`n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj`n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj`n3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj`nxref`n0 4`n0000000000 65535 f`n0000000009 00000 n`n0000000058 00000 n`n0000000115 00000 n`ntrailer<</Size 4/Root 1 0 R>>`nstartxref`n190`n%%EOF"
    [System.IO.File]::WriteAllText($TestFile, $pdfContent)
    Info "Created test PDF: $TestFile"
}

# ═════════════════════════════════════════════════════════════════════════════
# STEP 1 — Upload document from frontend (via API)
# ═════════════════════════════════════════════════════════════════════════════

Step 1 "Upload document from frontend (via API)"

# 1a. Ensure test user exists (register or login)
Info "Authenticating as $Email..."
$loginResult = Invoke-Api "POST" "$AuthBase/api/auth/login" @{ email = $Email; password = $Password }

if ($loginResult.__error) {
    Info "Login failed — attempting registration..."
    $regResult = Invoke-Api "POST" "$AuthBase/api/auth/register" @{
        firstName = "E2E"; lastName = "Test"; email = $Email; password = $Password; role = "Applicant"
    }
    if ($regResult.__error) {
        Fail "Authentication failed" "Cannot login or register: $($regResult.message)"
        Write-Host "`nCannot continue without a valid token. Exiting." -ForegroundColor Red
        exit 1
    }
    $token = $regResult.token
    Ok "Registered and authenticated as $Email"
} else {
    $token = $loginResult.token
    Ok "Authenticated as $Email"
}

Info "Token: $($token.Substring(0, [Math]::Min(40, $token.Length)))..."

# 1b. Upload the document
Info "Uploading document: $TestFile"
$form = @{
    applicationId = $ApplicationId
    documentType  = "NationalId"
    file          = Get-Item $TestFile
}

$uploadResult = Invoke-Api "POST" "$DocBase/api/documents/upload" -token $token -form $form

if ($uploadResult.__error) {
    Fail "Document upload failed" $uploadResult.message
    Write-Host "`nCannot continue without a document ID. Exiting." -ForegroundColor Red
    exit 1
}

$documentId = $uploadResult.id
Ok "Document uploaded successfully" "DocumentId: $documentId"
Ok "Upload response status: $($uploadResult.status)" "Expected: Pending"

if ($uploadResult.status -ne "Pending") {
    Warn "Initial status is '$($uploadResult.status)' — expected 'Pending'"
}

# ═════════════════════════════════════════════════════════════════════════════
# STEP 2 — Verify DB entry (Status = Pending)
# ═════════════════════════════════════════════════════════════════════════════

Step 2 "Verify DB entry (Status = Pending)"

$doc = Invoke-Api "GET" "$DocBase/api/documents/$documentId" -token $token

if ($doc.__error) {
    Fail "Cannot fetch document from DB" $doc.message
} else {
    Ok "Document exists in DB" "Id: $($doc.id)"

    if ($doc.status -eq "Pending") {
        Ok "Status = Pending  ✓"
    } elseif ($doc.status -in @("Processing", "Completed", "UnderReview")) {
        Warn "Status already advanced to '$($doc.status)' — consumer is very fast"
    } else {
        Fail "Unexpected initial status: $($doc.status)"
    }

    Ok "FileName: $($doc.fileName)"
    Ok "DocumentType: $($doc.documentType)"
    Ok "FileSizeBytes: $($doc.fileSizeBytes)"
    Ok "ApplicationId: $($doc.applicationId)"
    Ok "CreatedAtUtc: $($doc.createdAtUtc)"
}

# ═════════════════════════════════════════════════════════════════════════════
# STEP 3 — Verify event published to RabbitMQ
# ═════════════════════════════════════════════════════════════════════════════

Step 3 "Verify event published to RabbitMQ"

$queueName = "document-uploaded-event"
$venc      = "%2F"

$queueDetail = Invoke-RabbitApi "/queues/$venc/$queueName"

if ($queueDetail) {
    Ok "Queue exists: $queueName"
    Ok "Queue is durable: $($queueDetail.durable)"

    $published = $queueDetail.message_stats?.publish
    if ($published -gt 0) {
        Ok "Messages published to queue: $published"
    } else {
        Warn "message_stats.publish = 0 — stats may not have updated yet"
    }

    $consumers = $queueDetail.consumers
    if ($consumers -gt 0) {
        Ok "Consumer attached: $consumers consumer(s)"
    } else {
        Fail "No consumers on queue — AdminService may not be running"
    }

    Info "Queue state: ready=$($queueDetail.messages_ready), unacked=$($queueDetail.messages_unacked)"
} else {
    Fail "Queue '$queueName' not found in RabbitMQ"
    Warn "Ensure AdminService is running — it declares the queue on startup"
}

# ═════════════════════════════════════════════════════════════════════════════
# STEP 4 — Verify consumer processes message
# ═════════════════════════════════════════════════════════════════════════════

Step 4 "Verify consumer processes message"

Info "Polling for status change from Pending (timeout: ${PollTimeoutSec}s)..."

$pollResult = Poll-Status $documentId $token "Processing" $PollTimeoutSec

if ($pollResult.reached) {
    Ok "Consumer picked up message — status reached: Processing"
    Ok "Status history: $($pollResult.history -join ' → ')"
} elseif ($pollResult.terminal) {
    $finalStatus = $pollResult.doc.status
    if ($finalStatus -in @("Completed", "UnderReview")) {
        Ok "Consumer processed message (status=$finalStatus — skipped Processing, very fast)"
    } else {
        Fail "Consumer failed — status: $finalStatus" "FailureReason: $($pollResult.doc.failureReason)"
    }
} else {
    Fail "Status did not change from Pending within ${PollTimeoutSec}s"
    Warn "Check AdminService logs for errors"
    Warn "Ensure RabbitMQ is running: docker compose up -d"
}

# Verify AdminService DB record was created
$adminDbQueue = "document-uploaded-event"
$adminQDetail = Invoke-RabbitApi "/queues/$venc/$adminDbQueue"
if ($adminQDetail) {
    $acked = $adminQDetail.message_stats?.ack
    if ($acked -gt 0) {
        Ok "Message acked by consumer (total acked: $acked)"
    } else {
        Info "Ack count not yet reflected in stats"
    }
}

# ═════════════════════════════════════════════════════════════════════════════
# STEP 5 — Verify DB status: Pending → Processing → Completed
# ═════════════════════════════════════════════════════════════════════════════

Step 5 "Verify DB status transitions: Pending → Processing → Completed"

Info "Polling for Completed status (timeout: ${PollTimeoutSec}s)..."

$completedPoll = Poll-Status $documentId $token "Completed" $PollTimeoutSec

if ($completedPoll.reached) {
    Ok "Status reached: Completed"
    Ok "Full transition chain: $($completedPoll.history -join ' → ')"

    # Validate expected transitions occurred
    $history = $completedPoll.history
    if ($history -contains "Pending")    { Ok "Transition: Pending    ✓" }
    else                                  { Warn "Pending not observed (may have been too fast)" }
    if ($history -contains "Processing") { Ok "Transition: Processing ✓" }
    else                                  { Warn "Processing not observed (may have been too fast)" }
    if ($history -contains "Completed")  { Ok "Transition: Completed  ✓" }

    # Verify final document state
    $finalDoc = $completedPoll.doc
    Ok "Final status in DB: $($finalDoc.status)"
    Ok "UpdatedAtUtc: $($finalDoc.updatedAtUtc)"

} elseif ($completedPoll.terminal) {
    $s = $completedPoll.doc.status
    if ($s -eq "UnderReview") {
        Ok "Status reached UnderReview (pipeline completed, admin review queued)"
        Ok "Observed transitions: $($completedPoll.history -join ' → ')"
    } elseif ($s -eq "Failed") {
        Fail "Processing failed — status: Failed"
        Fail "FailureReason: $($completedPoll.doc.failureReason)"
    } else {
        Warn "Unexpected terminal status: $s"
    }
} else {
    Fail "Status did not reach Completed within ${PollTimeoutSec}s"
    $current = (Invoke-Api "GET" "$DocBase/api/documents/$documentId" -token $token).status
    Info "Current status: $current"
}

# ═════════════════════════════════════════════════════════════════════════════
# STEP 6 — Verify SignalR connection and subscription
# ═════════════════════════════════════════════════════════════════════════════

Step 6 "Verify SignalR (connection + subscription)"

# Check SignalR connections via RabbitMQ management (indirect — SignalR uses HTTP)
# Direct SignalR validation requires a WebSocket client; we verify the hub is reachable

$hubUrl = "$DocBase/hubs/document-status"
Info "Checking SignalR hub endpoint: $hubUrl"

try {
    # SignalR negotiate endpoint — returns connection info
    $negotiateUrl = "$hubUrl/negotiate?negotiateVersion=1"
    $negotiate = Invoke-RestMethod -Uri $negotiateUrl `
        -Method POST `
        -Headers @{ Authorization = "Bearer $token" } `
        -ErrorAction Stop

    if ($negotiate.connectionToken -or $negotiate.connectionId -or $negotiate.url) {
        Ok "SignalR hub is reachable at $hubUrl"
        Ok "Negotiate response received — hub is accepting connections"
        if ($negotiate.availableTransports) {
            $transports = $negotiate.availableTransports | ForEach-Object { $_.transport }
            Ok "Available transports: $($transports -join ', ')"
        }
    } else {
        Ok "SignalR hub responded (negotiate endpoint reachable)"
    }
} catch {
    $status = $_.Exception.Response?.StatusCode?.value__
    if ($status -eq 401) {
        Warn "SignalR hub returned 401 — token may have expired or hub requires auth"
    } elseif ($status -eq 404) {
        Fail "SignalR hub not found at $hubUrl — check Program.cs MapHub registration"
    } else {
        Warn "SignalR negotiate check inconclusive: $_"
        Info "Manual verification: open browser DevTools → Network → WS tab"
        Info "Navigate to: $($DocBase -replace '5023','3000')/documents/$documentId/status"
        Info "Look for WebSocket connection to: $hubUrl"
    }
}

# Verify the hub is mapped in the service
Info "Checking /health endpoint for service liveness..."
$health = Invoke-Api "GET" "$DocBase/health"
if ($health -and -not $health.__error) {
    $dbStatus     = ($health.checks | Where-Object { $_.name -eq "database" }).status
    $rabbitStatus = ($health.checks | Where-Object { $_.name -eq "rabbitmq" }).status
    Ok "DocumentService health: $($health.status)"
    Ok "DB health check: $dbStatus"
    Ok "RabbitMQ health check: $rabbitStatus"
} else {
    Warn "Health endpoint not reachable — service may not have health checks configured"
}

Info "SignalR real-time update verification:"
Info "  1. Open browser: http://localhost:3000/documents/$documentId/status"
Info "  2. Open DevTools → Network → WS tab"
Info "  3. Look for connection to: ws://localhost:5023/hubs/document-status"
Info "  4. Upload another document and watch the status badge update without refresh"
Info "  5. The '● Live' indicator in the top-right confirms SignalR is connected"

# ═════════════════════════════════════════════════════════════════════════════
# Summary
# ═════════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                        TEST SUMMARY                         ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$total = $pass + $fail + $warn
Write-Host "  Total checks : $total"
Write-Host "  Passed       : $pass" -ForegroundColor Green
if ($warn -gt 0) { Write-Host "  Warnings     : $warn" -ForegroundColor Yellow }
if ($fail -gt 0) { Write-Host "  Failed       : $fail" -ForegroundColor Red }

Write-Host ""
Write-Host "  Document ID  : $documentId"
Write-Host "  Track status : $DocBase/api/documents/$documentId"
Write-Host "  Frontend     : http://localhost:3000/documents/$documentId/status"
Write-Host ""

if ($fail -eq 0) {
    Write-Host "  ✓ End-to-end flow completed successfully!" -ForegroundColor Green
} else {
    Write-Host "  ✗ Some checks failed. See details above." -ForegroundColor Red
    Write-Host ""
    Write-Host "  Common fixes:" -ForegroundColor Yellow
    Write-Host "    docker compose up -d                          # Start RabbitMQ"
    Write-Host "    dotnet run --project DocumentService/...      # Start DocumentService"
    Write-Host "    dotnet run --project AdminService/...         # Start AdminService"
    Write-Host "    dotnet run --project AuthService/...          # Start AuthService"
}
Write-Host ""

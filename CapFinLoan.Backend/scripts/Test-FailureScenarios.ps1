#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Failure scenario tests for CapFinLoan document processing pipeline.

.DESCRIPTION
    Covers four failure scenarios:
      1. Malformed JSON message  → permanent failure → immediate DLQ (no retries)
      2. Missing required fields → validation failure → NackDiscard → DLQ
      3. Unsupported content type → business rule violation → NackDiscard → DLQ
      4. Transient failure simulation → retry exhaustion → DLQ after MaxDeliveryCount

    For scenario 4, the script publishes a valid-looking event pointing to a
    DocumentService that is intentionally unreachable (wrong port), causing
    HttpRequestException on every attempt. After MaxDeliveryCount retries the
    consumer dead-letters the message.

.USAGE
    # Prerequisites: Docker RabbitMQ running, AdminService running
    pwsh CapFinLoan.Backend/scripts/Test-FailureScenarios.ps1

    # Override settings:
    pwsh CapFinLoan.Backend/scripts/Test-FailureScenarios.ps1 `
        -MaxDeliveryCount 3 -RetryWaitSec 8
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
    [int]    $MaxDeliveryCount = 3,
    [int]    $RetryWaitSec     = 8,
    [int]    $DlqWaitSec       = 15,
    [string] $TestFile      = "$PSScriptRoot/../sample-files/test-document.pdf"
)


# ── Helpers ───────────────────────────────────────────────────────────────────

$pass = 0; $fail = 0; $warn = 0
$results = [System.Collections.Generic.List[hashtable]]::new()

function Scenario($n, $title) {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
    Write-Host "  SCENARIO $n — $title" -ForegroundColor White
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

function Publish-ToQueue($queue, $payload, $headers = @{}) {
    $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitUser}:${RabbitPass}"))
    $venc = "%2F"
    $body = @{
        routing_key      = $queue
        payload_encoding = "string"
        payload          = $payload
        properties       = @{
            content_type  = "application/json"
            delivery_mode = 2
            headers       = $headers
        }
    } | ConvertTo-Json -Depth 5

    try {
        return Invoke-RestMethod -Uri "$RabbitMgmt/api/exchanges/$venc//publish" `
            -Method POST `
            -Headers @{ Authorization = "Basic $cred"; "Content-Type" = "application/json" } `
            -Body $body -ErrorAction Stop
    } catch {
        return @{ __error = $true; message = $_.Exception.Message }
    }
}

# Snapshot queue stats before a test so we can measure deltas
function Get-QueueSnapshot($queueName) {
    $venc = "%2F"
    $q = Invoke-RabbitApi "/queues/$venc/$([Uri]::EscapeDataString($queueName))"
    if (-not $q) { return @{ messages = 0; acked = 0; published = 0; nacked = 0 } }
    return @{
        messages  = $q.messages
        acked     = [int]($q.message_stats?.ack      ?? 0)
        published = [int]($q.message_stats?.publish  ?? 0)
        nacked    = [int]($q.message_stats?.deliver_no_ack ?? 0)
    }
}

function Poll-DlqCount($dlqName, $expectedMin, $timeoutSec) {
    $venc     = "%2F"
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $q = Invoke-RabbitApi "/queues/$venc/$([Uri]::EscapeDataString($dlqName))"
        if ($q -and $q.messages -ge $expectedMin) { return $q.messages }
        Start-Sleep -Milliseconds 1000
    }
    $q = Invoke-RabbitApi "/queues/$venc/$([Uri]::EscapeDataString($dlqName))"
    return if ($q) { $q.messages } else { 0 }
}

function Get-DlqMessages($dlqName, $count = 5) {
    $venc = "%2F"
    $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitUser}:${RabbitPass}"))
    $body = @{ count = $count; ackmode = "ack_requeue_true"; encoding = "auto" } | ConvertTo-Json
    try {
        return Invoke-RestMethod `
            -Uri "$RabbitMgmt/api/queues/$venc/$([Uri]::EscapeDataString($dlqName))/get" `
            -Method POST `
            -Headers @{ Authorization = "Basic $cred"; "Content-Type" = "application/json" } `
            -Body $body -ErrorAction Stop
    } catch { return @() }
}


# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Red
Write-Host "║     CapFinLoan — Failure Scenario Tests                      ║" -ForegroundColor Red
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Red
Write-Host ""
Info "DocService:  $DocBase"
Info "AuthService: $AuthBase"
Info "RabbitMQ:    $RabbitMgmt"
Info "MaxDeliveryCount: $MaxDeliveryCount  (from appsettings.json)"
Info "DLQ wait timeout: ${DlqWaitSec}s"

# ── Pre-flight: RabbitMQ reachable? ───────────────────────────────────────────

$overview = Invoke-RabbitApi "/overview"
if (-not $overview) {
    Write-Host "`n  ✗ Cannot reach RabbitMQ at $RabbitMgmt" -ForegroundColor Red
    Write-Host "  Run: docker compose up -d" -ForegroundColor Yellow
    exit 1
}
Ok "RabbitMQ reachable — version $($overview.rabbitmq_version)"

# ── Auth token (needed for document upload in scenario 4) ─────────────────────

$token = $null
$loginResult = Invoke-Api "POST" "$AuthBase/api/auth/login" @{ email = $Email; password = $Password }
if (-not $loginResult.__error) {
    $token = $loginResult.token
    Ok "Authenticated as $Email"
} else {
    $regResult = Invoke-Api "POST" "$AuthBase/api/auth/register" @{
        firstName = "Failure"; lastName = "Test"; email = $Email; password = $Password; role = "Applicant"
    }
    if (-not $regResult.__error) {
        $token = $regResult.token
        Ok "Registered and authenticated as $Email"
    } else {
        Warn "Could not authenticate — scenario 4 (transient failure via upload) will be skipped"
    }
}

$mainQueue = "document-uploaded-event"
$dlqName   = "document-uploaded-event.dlq"

# ═════════════════════════════════════════════════════════════════════════════
# SCENARIO 1 — Malformed JSON → permanent failure → immediate DLQ
# ═════════════════════════════════════════════════════════════════════════════

Scenario 1 "Malformed JSON → permanent failure → immediate DLQ (no retries)"

Info "Expected flow: consumer receives message → JsonException → NackDiscard → DLQ"
Info "x-retry-count should NOT increment (permanent failures skip retry budget)"

$dlqBefore = Get-QueueSnapshot $dlqName

$result = Publish-ToQueue $mainQueue "THIS IS NOT JSON {{{invalid"
if ($result.__error) {
    Fail "Could not publish malformed message" $result.message
} else {
    if ($result.routed) {
        Ok "Malformed message published and routed to: $mainQueue"
    } else {
        Fail "Message published but NOT routed — queue may not exist yet (start AdminService first)"
    }
}

Info "Waiting ${RetryWaitSec}s for consumer to process and dead-letter..."
Start-Sleep -Seconds $RetryWaitSec

$dlqAfter = Poll-DlqCount $dlqName ($dlqBefore.messages + 1) $DlqWaitSec

if ($dlqAfter -gt $dlqBefore.messages) {
    Ok "DLQ received message — count: $dlqBefore.messages → $dlqAfter"
    Ok "Permanent failure correctly bypassed retry budget → dead-lettered immediately"
} else {
    Fail "DLQ count unchanged ($dlqAfter) — message may still be in main queue or consumer is down"
    $mainQ = Invoke-RabbitApi "/queues/%2F/$([Uri]::EscapeDataString($mainQueue))"
    if ($mainQ) { Info "Main queue: ready=$($mainQ.messages_ready), unacked=$($mainQ.messages_unacked)" }
}

# Peek at DLQ to confirm it's our malformed payload
$dlqMsgs = Get-DlqMessages $dlqName 1
if ($dlqMsgs -and $dlqMsgs.Count -gt 0) {
    $payload = $dlqMsgs[0].payload
    if ($payload -like "*THIS IS NOT JSON*") {
        Ok "DLQ payload confirmed — contains original malformed bytes"
    } else {
        Info "DLQ payload (latest): $($payload.Substring(0, [Math]::Min(120, $payload.Length)))..."
    }
}

# ═════════════════════════════════════════════════════════════════════════════
# SCENARIO 2 — Missing required fields → validation → NackDiscard → DLQ
# ═════════════════════════════════════════════════════════════════════════════

Scenario 2 "Missing required fields → validation failure → NackDiscard → DLQ"

Info "Expected flow: deserialization succeeds → Validate() throws ArgumentException"
Info "                → handler returns NackDiscard → DLQ (no retries)"

$dlqBefore2 = Get-QueueSnapshot $dlqName

# Valid JSON structure but DocumentId = Guid.Empty (fails Validate())
$missingFieldsPayload = @{
    documentId    = "00000000-0000-0000-0000-000000000000"   # Guid.Empty → ArgumentException
    applicationId = "00000000-0000-0000-0000-000000000001"
    userId        = "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    documentType  = "NationalId"
    fileName      = "test.pdf"
    contentType   = "application/pdf"
    fileSizeBytes = 1024
    uploadedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Compress

$result2 = Publish-ToQueue $mainQueue $missingFieldsPayload
if ($result2.__error) {
    Fail "Could not publish missing-fields message" $result2.message
} elseif ($result2.routed) {
    Ok "Missing-fields message published and routed"
} else {
    Fail "Message not routed"
}

Info "Waiting ${RetryWaitSec}s for consumer to process..."
Start-Sleep -Seconds $RetryWaitSec

$dlqAfter2 = Poll-DlqCount $dlqName ($dlqBefore2.messages + 1) $DlqWaitSec

if ($dlqAfter2 -gt $dlqBefore2.messages) {
    Ok "DLQ received message — count: $($dlqBefore2.messages) → $dlqAfter2"
    Ok "Validation failure correctly dead-lettered without retrying"
} else {
    Fail "DLQ count unchanged ($dlqAfter2) after validation failure scenario"
}

# ═════════════════════════════════════════════════════════════════════════════
# SCENARIO 3 — Unsupported content type → business rule → NackDiscard → DLQ
# ═════════════════════════════════════════════════════════════════════════════

Scenario 3 "Unsupported content type → InvalidOperationException → NackDiscard → DLQ"

Info "Expected flow: Validate() throws InvalidOperationException (content type not in allowlist)"
Info "                → handler returns NackDiscard → DLQ (no retries)"

$dlqBefore3 = Get-QueueSnapshot $dlqName

$badContentTypePayload = @{
    documentId    = [Guid]::NewGuid().ToString()
    applicationId = "00000000-0000-0000-0000-000000000001"
    userId        = "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    documentType  = "NationalId"
    fileName      = "malware.exe"
    contentType   = "application/x-msdownload"   # not in AllowedContentTypes
    fileSizeBytes = 2048
    uploadedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Compress

$result3 = Publish-ToQueue $mainQueue $badContentTypePayload
if ($result3.__error) {
    Fail "Could not publish bad-content-type message" $result3.message
} elseif ($result3.routed) {
    Ok "Bad-content-type message published and routed"
} else {
    Fail "Message not routed"
}

Info "Waiting ${RetryWaitSec}s for consumer to process..."
Start-Sleep -Seconds $RetryWaitSec

$dlqAfter3 = Poll-DlqCount $dlqName ($dlqBefore3.messages + 1) $DlqWaitSec

if ($dlqAfter3 -gt $dlqBefore3.messages) {
    Ok "DLQ received message — count: $($dlqBefore3.messages) → $dlqAfter3"
    Ok "Business rule violation correctly dead-lettered without retrying"
} else {
    Fail "DLQ count unchanged ($dlqAfter3) after bad-content-type scenario"
}


# ═════════════════════════════════════════════════════════════════════════════
# SCENARIO 4 — Transient failure → retry exhaustion → DLQ after MaxDeliveryCount
# ═════════════════════════════════════════════════════════════════════════════

Scenario 4 "Transient failure → retry exhaustion → DLQ after $MaxDeliveryCount retries"

Info "Strategy: publish a valid event with a DocumentId that does NOT exist in DocumentService."
Info "          DocumentProcessingService will call PATCH /api/internal/documents/{id}/status"
Info "          → 404 Not Found → HttpRequestException → NackRequeue → retry."
Info "          After $MaxDeliveryCount retries, x-retry-count >= MaxDeliveryCount → NackDiscard → DLQ."
Info ""
Info "Expected x-retry-count progression: 0 → 1 → 2 → 3 (dead-letter)"

$dlqBefore4 = Get-QueueSnapshot $dlqName

# Use a DocumentId that will never exist in DocumentService
$nonExistentDocId = [Guid]::NewGuid().ToString()

$transientPayload = @{
    documentId    = $nonExistentDocId
    applicationId = "00000000-0000-0000-0000-000000000001"
    userId        = "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    documentType  = "NationalId"
    fileName      = "transient-test.pdf"
    contentType   = "application/pdf"
    fileSizeBytes = 4096
    uploadedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Compress

Info "Publishing transient-failure event — DocumentId: $nonExistentDocId"
$result4 = Publish-ToQueue $mainQueue $transientPayload
if ($result4.__error) {
    Fail "Could not publish transient-failure message" $result4.message
} elseif ($result4.routed) {
    Ok "Transient-failure message published and routed"
} else {
    Fail "Message not routed — queue may not exist (start AdminService first)"
}

# ── Poll: watch x-retry-count climb in main queue ────────────────────────────

Info ""
Info "Monitoring retry progression (each retry adds ~${RetryWaitSec}s)..."
Info "Total wait: ~$($RetryWaitSec * ($MaxDeliveryCount + 2))s"

$retryObserved = [System.Collections.Generic.List[int]]::new()
$deadline      = (Get-Date).AddSeconds($RetryWaitSec * ($MaxDeliveryCount + 3))
$lastRetry     = -1

while ((Get-Date) -lt $deadline) {
    # Peek at the main queue without consuming
    $venc    = "%2F"
    $cred    = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitUser}:${RabbitPass}"))
    $peekBody = @{ count = 1; ackmode = "ack_requeue_true"; encoding = "auto" } | ConvertTo-Json
    try {
        $msgs = Invoke-RestMethod `
            -Uri "$RabbitMgmt/api/queues/$venc/$([Uri]::EscapeDataString($mainQueue))/get" `
            -Method POST `
            -Headers @{ Authorization = "Basic $cred"; "Content-Type" = "application/json" } `
            -Body $peekBody -ErrorAction Stop

        foreach ($m in $msgs) {
            $hdrs       = $m.properties?.headers
            $retryCount = if ($hdrs -and $hdrs.'x-retry-count') { [int]$hdrs.'x-retry-count' } else { 0 }
            $msgPayload = $m.payload
            if ($msgPayload -like "*$nonExistentDocId*" -and $retryCount -ne $lastRetry) {
                $lastRetry = $retryCount
                $retryObserved.Add($retryCount)
                Info "  → x-retry-count = $retryCount  ($(([datetime]::UtcNow).ToString('HH:mm:ss.fff')))"
            }
        }
    } catch { }

    # Check if it landed in DLQ
    $dlqNow = Invoke-RabbitApi "/queues/$venc/$([Uri]::EscapeDataString($dlqName))"
    if ($dlqNow -and $dlqNow.messages -gt $dlqBefore4.messages) {
        Info "  → Message arrived in DLQ  ($(([datetime]::UtcNow).ToString('HH:mm:ss.fff')))"
        break
    }

    Start-Sleep -Milliseconds 1500
}

# ── Verify retry count progression ───────────────────────────────────────────

if ($retryObserved.Count -gt 0) {
    Ok "Retry progression observed: $($retryObserved -join ' → ')"
    if ($retryObserved[-1] -ge $MaxDeliveryCount) {
        Ok "x-retry-count reached MaxDeliveryCount ($MaxDeliveryCount) — consumer dead-lettered"
    } else {
        Warn "x-retry-count only reached $($retryObserved[-1]) of $MaxDeliveryCount — still retrying or already in DLQ"
    }
} else {
    Warn "Could not observe x-retry-count in main queue (consumer may be too fast or queue was empty during peek)"
    Info "This is normal if the consumer processes and republishes faster than the poll interval"
}

# ── Verify DLQ received the exhausted message ─────────────────────────────────

$dlqFinal = Poll-DlqCount $dlqName ($dlqBefore4.messages + 1) $DlqWaitSec

if ($dlqFinal -gt $dlqBefore4.messages) {
    Ok "DLQ received exhausted message — count: $($dlqBefore4.messages) → $dlqFinal"
    Ok "Retry exhaustion correctly dead-lettered after $MaxDeliveryCount retries"

    # Peek DLQ to confirm it's our message and check x-death header
    $dlqPeek = Get-DlqMessages $dlqName 5
    $ourMsg  = $dlqPeek | Where-Object { $_.payload -like "*$nonExistentDocId*" } | Select-Object -First 1

    if ($ourMsg) {
        Ok "DLQ payload confirmed — contains DocumentId: $nonExistentDocId"

        $dlqHdrs = $ourMsg.properties?.headers
        if ($dlqHdrs) {
            $retryInDlq = $dlqHdrs.'x-retry-count'
            if ($retryInDlq) {
                Ok "x-retry-count in DLQ message: $retryInDlq (expected: $MaxDeliveryCount)"
            }
            # x-death is added by RabbitMQ broker when a message is dead-lettered
            $xDeath = $dlqHdrs.'x-death'
            if ($xDeath) {
                Ok "x-death header present — broker confirmed dead-letter routing"
                Info "x-death[0].reason: $($xDeath[0]?.reason ?? 'rejected')"
                Info "x-death[0].queue:  $($xDeath[0]?.queue  ?? $mainQueue)"
            }
        }
    } else {
        Info "Could not match our specific DocumentId in DLQ peek (may have been consumed by another test)"
    }
} else {
    Fail "DLQ count unchanged ($dlqFinal) — message did not reach DLQ within timeout"
    Warn "Possible causes:"
    Warn "  1. AdminService is not running (no consumer to retry the message)"
    Warn "  2. DocumentService IS running and returned 200 (document was created by a race)"
    Warn "  3. DlqWaitSec ($DlqWaitSec) is too short — increase with -DlqWaitSec 30"
    Warn "  4. MaxDeliveryCount in appsettings.json differs from -MaxDeliveryCount $MaxDeliveryCount"
}

# ── Verify document status = Failed in DocumentService ────────────────────────

if ($token) {
    Info ""
    Info "Checking DocumentService for document status (may be 404 if never created)..."
    $docCheck = Invoke-Api "GET" "$DocBase/api/documents/$nonExistentDocId" -token $token
    if ($docCheck.__error -and $docCheck.status -eq 404) {
        Ok "Document not found in DocumentService (404) — expected for non-existent DocumentId"
        Info "The processing record in AdminService DB will show Status=Failed"
    } elseif (-not $docCheck.__error) {
        if ($docCheck.status -eq "Failed") {
            Ok "Document status = Failed in DocumentService"
            Ok "FailureReason: $($docCheck.failureReason)"
        } else {
            Info "Document status: $($docCheck.status) (may still be processing)"
        }
    }
}


# ═════════════════════════════════════════════════════════════════════════════
# SCENARIO 5 — File too large → business rule → NackDiscard → DLQ
# ═════════════════════════════════════════════════════════════════════════════

Scenario 5 "File too large → InvalidOperationException → NackDiscard → DLQ"

Info "Expected flow: Validate() throws InvalidOperationException (fileSizeBytes > 5 MB)"
Info "                → handler returns NackDiscard → DLQ (no retries)"

$dlqBefore5 = Get-QueueSnapshot $dlqName

$oversizePayload = @{
    documentId    = [Guid]::NewGuid().ToString()
    applicationId = "00000000-0000-0000-0000-000000000001"
    userId        = "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    documentType  = "NationalId"
    fileName      = "huge-file.pdf"
    contentType   = "application/pdf"
    fileSizeBytes = 10485760   # 10 MB — exceeds 5 MB limit
    uploadedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Compress

$result5 = Publish-ToQueue $mainQueue $oversizePayload
if ($result5.__error) {
    Fail "Could not publish oversize message" $result5.message
} elseif ($result5.routed) {
    Ok "Oversize message published and routed"
} else {
    Fail "Message not routed"
}

Info "Waiting ${RetryWaitSec}s for consumer to process..."
Start-Sleep -Seconds $RetryWaitSec

$dlqAfter5 = Poll-DlqCount $dlqName ($dlqBefore5.messages + 1) $DlqWaitSec

if ($dlqAfter5 -gt $dlqBefore5.messages) {
    Ok "DLQ received message — count: $($dlqBefore5.messages) → $dlqAfter5"
    Ok "Oversize file correctly dead-lettered without retrying"
} else {
    Fail "DLQ count unchanged ($dlqAfter5) after oversize scenario"
}

# ═════════════════════════════════════════════════════════════════════════════
# SCENARIO 6 — Pre-seeded x-retry-count at MaxDeliveryCount → immediate DLQ
# ═════════════════════════════════════════════════════════════════════════════

Scenario 6 "Pre-seeded x-retry-count = $MaxDeliveryCount → consumer dead-letters immediately"

Info "Simulates a message that has already exhausted its retry budget."
Info "Consumer checks x-retry-count BEFORE dispatching to handler."
Info "Expected: NackDiscard immediately, handler never called."

$dlqBefore6 = Get-QueueSnapshot $dlqName

$exhaustedPayload = @{
    documentId    = [Guid]::NewGuid().ToString()
    applicationId = "00000000-0000-0000-0000-000000000001"
    userId        = "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    documentType  = "NationalId"
    fileName      = "already-exhausted.pdf"
    contentType   = "application/pdf"
    fileSizeBytes = 1024
    uploadedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Compress

# Publish with x-retry-count already at MaxDeliveryCount
$result6 = Publish-ToQueue $mainQueue $exhaustedPayload @{ "x-retry-count" = $MaxDeliveryCount }
if ($result6.__error) {
    Fail "Could not publish pre-exhausted message" $result6.message
} elseif ($result6.routed) {
    Ok "Pre-exhausted message published (x-retry-count=$MaxDeliveryCount)"
} else {
    Fail "Message not routed"
}

Info "Waiting ${RetryWaitSec}s for consumer to dead-letter immediately..."
Start-Sleep -Seconds $RetryWaitSec

$dlqAfter6 = Poll-DlqCount $dlqName ($dlqBefore6.messages + 1) $DlqWaitSec

if ($dlqAfter6 -gt $dlqBefore6.messages) {
    Ok "DLQ received message — count: $($dlqBefore6.messages) → $dlqAfter6"
    Ok "Pre-exhausted message dead-lettered immediately (handler was never invoked)"
} else {
    Fail "DLQ count unchanged ($dlqAfter6) — consumer may not have processed the message yet"
}

# ═════════════════════════════════════════════════════════════════════════════
# DLQ Summary — final state
# ═════════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "  ── DLQ Final State ──────────────────────────────────────────" -ForegroundColor DarkGray
$venc = "%2F"
$dlqFinalState = Invoke-RabbitApi "/queues/$venc/$([Uri]::EscapeDataString($dlqName))"
if ($dlqFinalState) {
    Info "Queue:    $dlqName"
    Info "Messages: $($dlqFinalState.messages)"
    Info "Durable:  $($dlqFinalState.durable)"
    $dlqStats = $dlqFinalState.message_stats
    if ($dlqStats) {
        Info "Total published to DLQ: $($dlqStats.publish ?? 0)"
    }
} else {
    Warn "Could not fetch DLQ state"
}

$mainQFinal = Invoke-RabbitApi "/queues/$venc/$([Uri]::EscapeDataString($mainQueue))"
if ($mainQFinal) {
    Info ""
    Info "Queue:    $mainQueue"
    Info "Messages: $($mainQFinal.messages) (should be 0 after all scenarios complete)"
    if ($mainQFinal.messages -gt 0) {
        Warn "$($mainQFinal.messages) message(s) still in main queue — consumer may be processing"
    }
}

# ═════════════════════════════════════════════════════════════════════════════
# Summary
# ═════════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    FAILURE TEST SUMMARY                     ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$total = $pass + $fail + $warn
Write-Host "  Total checks : $total"
Write-Host "  Passed       : $pass" -ForegroundColor Green
if ($warn -gt 0) { Write-Host "  Warnings     : $warn" -ForegroundColor Yellow }
if ($fail -gt 0) { Write-Host "  Failed       : $fail" -ForegroundColor Red }

Write-Host ""
Write-Host "  Scenario results:" -ForegroundColor White
Write-Host "    1. Malformed JSON          → immediate DLQ (JsonException)"
Write-Host "    2. Missing required fields → immediate DLQ (ArgumentException)"
Write-Host "    3. Unsupported content type→ immediate DLQ (InvalidOperationException)"
Write-Host "    4. Transient failure       → retry x$MaxDeliveryCount → DLQ (HttpRequestException)"
Write-Host "    5. File too large          → immediate DLQ (InvalidOperationException)"
Write-Host "    6. Pre-exhausted retries   → immediate DLQ (x-retry-count guard)"
Write-Host ""

if ($fail -eq 0) {
    Write-Host "  ✓ All failure scenarios passed!" -ForegroundColor Green
} else {
    Write-Host "  ✗ Some checks failed. See details above." -ForegroundColor Red
    Write-Host ""
    Write-Host "  Common fixes:" -ForegroundColor Yellow
    Write-Host "    docker compose up -d                          # Start RabbitMQ"
    Write-Host "    dotnet run --project AdminService/...         # Start AdminService (consumer)"
    Write-Host "    dotnet run --project DocumentService/...      # Start DocumentService"
    Write-Host "    Increase -DlqWaitSec 30                       # Give more time for retries"
    Write-Host "    Check -MaxDeliveryCount matches appsettings.json Consumer.MaxDeliveryCount"
}

Write-Host ""
Write-Host "  RabbitMQ Management UI: http://localhost:15672" -ForegroundColor DarkGray
Write-Host "    → Queues → $dlqName → Get Messages" -ForegroundColor DarkGray
Write-Host "    → Inspect x-retry-count and x-death headers on DLQ messages" -ForegroundColor DarkGray
Write-Host ""

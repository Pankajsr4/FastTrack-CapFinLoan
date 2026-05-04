#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates the RabbitMQ setup for CapFinLoan.
    Checks: connection, queues, DLQ/DLX topology, publish, consume, DLQ routing.

.USAGE
    # Run from repo root:
    pwsh CapFinLoan.Backend/scripts/Validate-RabbitMQ.ps1

    # Override credentials:
    pwsh CapFinLoan.Backend/scripts/Validate-RabbitMQ.ps1 -User myuser -Pass mypass
#>

param(
    [string] $Host  = "localhost",
    [int]    $Port  = 15672,
    [string] $User  = "capfinloan",
    [string] $Pass  = "capfinloan123",
    [string] $VHost = "/"
)

$base    = "http://${Host}:${Port}/api"
$venc    = [Uri]::EscapeDataString($VHost)
$headers = @{ Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${User}:${Pass}")) }

$pass = 0; $fail = 0

function Ok($msg)   { Write-Host "  [PASS] $msg" -ForegroundColor Green;  $script:pass++ }
function Fail($msg) { Write-Host "  [FAIL] $msg" -ForegroundColor Red;    $script:fail++ }
function Info($msg) { Write-Host "  [INFO] $msg" -ForegroundColor Cyan }
function Section($title) { Write-Host "`n=== $title ===" -ForegroundColor Yellow }

function Get-Api($path) {
    try { Invoke-RestMethod -Uri "$base$path" -Headers $headers -ErrorAction Stop }
    catch { $null }
}

# ── 1. Connection ─────────────────────────────────────────────────────────────
Section "1. Connection"

$overview = Get-Api "/overview"
if ($overview) {
    Ok "Management API reachable at ${Host}:${Port}"
    Info "RabbitMQ version: $($overview.rabbitmq_version)"
    $conns = $overview.object_totals.connections
    if ($conns -gt 0) { Ok "Active connections: $conns" }
    else              { Fail "No active connections — are the services running?" }
} else {
    Fail "Cannot reach Management API at ${Host}:${Port} — is Docker running?"
    Write-Host "`nRun: docker compose up -d" -ForegroundColor Yellow
    exit 1
}

# ── 2. Queues created ─────────────────────────────────────────────────────────
Section "2. Queue topology"

$expectedQueues = @(
    "document-uploaded-event",
    "document-uploaded-event.dlq",
    "document-verified-event",
    "document-verified-event.dlq",
    "application-status-changed-event",
    "application-status-changed-event.dlq",
    "application-submitted-event",
    "application-submitted-event.dlq",
    "user-registered-event",
    "user-registered-event.dlq"
)

$allQueues = Get-Api "/queues/$venc"
$queueNames = $allQueues | ForEach-Object { $_.name }

foreach ($q in $expectedQueues) {
    if ($queueNames -contains $q) { Ok "Queue exists: $q" }
    else                          { Fail "Queue missing: $q  (start the relevant service)" }
}

# Check durable flag on main queues
foreach ($q in $expectedQueues | Where-Object { -not $_.EndsWith(".dlq") }) {
    $detail = Get-Api "/queues/$venc/$([Uri]::EscapeDataString($q))"
    if ($detail) {
        if ($detail.durable) { Ok "Durable=true: $q" }
        else                 { Fail "Durable=false: $q  (queue will not survive restart)" }

        $dlx = $detail.arguments.'x-dead-letter-exchange'
        if ($dlx) { Ok "DLX configured: $q → $dlx" }
        else      { Fail "No x-dead-letter-exchange on: $q" }
    }
}

# ── 3. DLX exchanges ──────────────────────────────────────────────────────────
Section "3. Dead-letter exchanges"

$expectedExchanges = @(
    "document-uploaded-event.dlx",
    "document-verified-event.dlx",
    "application-status-changed-event.dlx"
)

$allExchanges = Get-Api "/exchanges/$venc"
$exchangeNames = $allExchanges | ForEach-Object { $_.name }

foreach ($ex in $expectedExchanges) {
    if ($exchangeNames -contains $ex) {
        $detail = Get-Api "/exchanges/$venc/$([Uri]::EscapeDataString($ex))"
        if ($detail.type -eq "fanout") { Ok "DLX fanout: $ex" }
        else                           { Fail "DLX wrong type ($($detail.type)): $ex  (expected fanout)" }
    } else {
        Fail "DLX missing: $ex"
    }
}

# ── 4. Publish a test message ─────────────────────────────────────────────────
Section "4. Publish test message"

$testQueue = "document-uploaded-event"
$payload   = @{
    documentId    = [Guid]::NewGuid().ToString()
    applicationId = "00000000-0000-0000-0000-000000000001"
    userId        = "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    documentType  = "NationalId"
    fileName      = "validation-test.pdf"
    contentType   = "application/pdf"
    fileSizeBytes = 12345
    uploadedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Compress

$publishBody = @{
    routing_key      = $testQueue
    payload_encoding = "string"
    payload          = $payload
    properties       = @{
        content_type  = "application/json"
        delivery_mode = 2
        headers       = @{}
    }
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "$base/exchanges/$venc//publish" `
        -Method POST -Headers $headers `
        -ContentType "application/json" -Body $publishBody -ErrorAction Stop

    if ($result.routed -eq $true) { Ok "Message published and routed to: $testQueue" }
    else                          { Fail "Message published but NOT routed (queue may not exist)" }
} catch {
    Fail "Publish failed: $_"
}

# ── 5. Verify message consumed ────────────────────────────────────────────────
Section "5. Message consumption"

Start-Sleep -Milliseconds 2000   # give consumer time to process

$qDetail = Get-Api "/queues/$venc/$([Uri]::EscapeDataString($testQueue))"
if ($qDetail) {
    $ready   = $qDetail.messages_ready
    $unacked = $qDetail.messages_unacked
    $consum  = $qDetail.consumers

    if ($consum -gt 0) { Ok "Consumer attached: $consum consumer(s) on $testQueue" }
    else               { Fail "No consumers on $testQueue  (AdminService not running?)" }

    if ($ready -eq 0 -and $unacked -eq 0) {
        Ok "Message consumed and acked (messages_ready=0, messages_unacked=0)"
    } elseif ($ready -gt 0) {
        Fail "Message still in queue (messages_ready=$ready) — consumer may be down"
    } elseif ($unacked -gt 0) {
        Info "Message in-flight (messages_unacked=$unacked) — processing..."
    }

    $acked = $qDetail.message_stats.ack
    if ($acked -gt 0) { Ok "Total acked messages: $acked" }
} else {
    Fail "Could not fetch queue details for $testQueue"
}

# ── 6. DLQ behavior ───────────────────────────────────────────────────────────
Section "6. DLQ behavior"

$dlqName = "document-uploaded-event.dlq"

# Publish a malformed message to trigger DLQ routing
$badPayload = @{
    routing_key      = $testQueue
    payload_encoding = "string"
    payload          = "INVALID_JSON_PAYLOAD_FOR_DLQ_TEST"
    properties       = @{
        content_type  = "application/json"
        delivery_mode = 2
        headers       = @{}
    }
} | ConvertTo-Json

try {
    $r = Invoke-RestMethod -Uri "$base/exchanges/$venc//publish" `
        -Method POST -Headers $headers `
        -ContentType "application/json" -Body $badPayload -ErrorAction Stop

    if ($r.routed) {
        Info "Malformed message published — waiting for DLQ routing (up to 5s)..."
        Start-Sleep -Seconds 5

        $dlqDetail = Get-Api "/queues/$venc/$([Uri]::EscapeDataString($dlqName))"
        if ($dlqDetail -and $dlqDetail.messages -gt 0) {
            Ok "DLQ received failed message: $($dlqDetail.messages) message(s) in $dlqName"
            Info "DLQ routing is working correctly"
        } else {
            $dlqMsgs = if ($dlqDetail) { $dlqDetail.messages } else { "N/A" }
            Fail "DLQ message count: $dlqMsgs  (expected > 0 after malformed publish)"
            Info "Possible reasons: AdminService not running, or consumer not started yet"
        }
    }
} catch {
    Fail "Could not publish malformed message: $_"
}

# ── Summary ───────────────────────────────────────────────────────────────────
Section "Summary"
$total = $pass + $fail
Write-Host ""
Write-Host "  Passed: $pass / $total" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
if ($fail -gt 0) {
    Write-Host "  Failed: $fail / $total" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Troubleshooting:" -ForegroundColor Yellow
    Write-Host "    1. Ensure Docker is running:  docker compose up -d"
    Write-Host "    2. Start all services:        dotnet run (in each service folder)"
    Write-Host "    3. Management UI:             http://localhost:15672  (capfinloan/capfinloan123)"
    Write-Host "    4. Check service logs for connection errors"
}
Write-Host ""

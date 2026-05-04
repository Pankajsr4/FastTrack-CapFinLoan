# Stop all CapFinLoan Microservices and the API Gateway

Write-Host "Stopping CapFinLoan Microservices..." -ForegroundColor Cyan

# List of process names to stop (as started by start-all.ps1)
$processNames = @(
    "CapFinLoan.Auth.API",
    "CapFinLoan.Application.API",
    "CapFinLoan.Admin.API",
    "CapFinLoan.Document.API",
    "CapFinLoan.Gateway.API"
)

foreach ($name in $processNames) {
    $procs = Get-Process | Where-Object { $_.ProcessName -like $name }
    foreach ($proc in $procs) {
        Write-Host "Stopping $($proc.ProcessName) (PID: $($proc.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $proc.Id -Force
    }
}

Write-Host "All services stopped!" -ForegroundColor Green

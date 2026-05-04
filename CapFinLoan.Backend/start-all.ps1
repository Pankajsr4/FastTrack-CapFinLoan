# Start all Microservices and the API Gateway in the background

Write-Host "Starting CapFinLoan Microservices..." -ForegroundColor Cyan

# Define paths
$basePath = $PSScriptRoot
$solutionPath = Join-Path $basePath "CapFinLoan.Backend.slnx"
$services = @(
    @{ Name = "Auth Service"; Path = "AuthService\CapFinLoan.Auth.API" },
    @{ Name = "Application Service"; Path = "ApplicationService\CapFinLoan.Application.API" },
    @{ Name = "Admin Service"; Path = "AdminService\CapFinLoan.Admin.API" },
    @{ Name = "Document Service"; Path = "DocumentService\CapFinLoan.Document.API" },
    @{ Name = "API Gateway"; Path = "ApiGateway\CapFinLoan.Gateway.API" }
)

Write-Host "Building solution once before launching services..." -ForegroundColor Yellow
dotnet build "$solutionPath" --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. Services were not started." -ForegroundColor Red
    exit $LASTEXITCODE
}

foreach ($service in $services) {
    Write-Host "Starting $($service.Name)..." -ForegroundColor Yellow
    $fullPath = Join-Path $basePath $service.Path
    Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run --no-build --project `"$fullPath`""
}

Write-Host "All services started!" -ForegroundColor Green
Write-Host "Auth Service: http://localhost:5021"
Write-Host "Application Service: http://localhost:5022"
Write-Host "Document Service: http://localhost:5023"
Write-Host "Admin Service: http://localhost:5024"
Write-Host "API Gateway: http://localhost:5020" -ForegroundColor Magenta
